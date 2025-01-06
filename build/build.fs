open Fake.Core
open Fake.BuildServer
open Fake.DotNet
open Fake.DotNet.Testing
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open System
open System.IO
open System.Text
open System.Text.RegularExpressions

// The name of the project
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "pbi-tools"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "Power BI source control and DevOps tool."

// Longer description of the project
// (used as a description for NuGet package; line breaks are automatically cleaned up)
let description = "A command-line tool to work with Power BI Desktop files. Enables change tracking for PBIx files in source control as well as generating those files programmatically."

// List of author names (for NuGet package)
let authors = [ "Mathias Thierbach" ]
let company = "pbi-tools Ltd"

// Tags for your project (for NuGet package)
let tags = "powerbi, pbix, source-control, automation"

// File system information
let solutionFile  = "PBI-TOOLS.sln"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "pbi-tools"
let gitHome = "https://github.com/" + gitOwner

// The name of the project on GitHub
let gitName = "pbi-tools"

// The url for the raw files hosted
let gitRaw = Environment.environVarOrDefault "gitRaw" ("https://raw.github.com/" + gitOwner)

let buildDir = ".build"
let outDir = buildDir @@ "out"
let distDir = buildDir @@ "dist"
let distFullDir = distDir @@ "desktop"
let distCoreDir = distDir @@ "core"
let netXLabel   = "net9"
let distNetXDir = distDir @@ netXLabel
let testDir = buildDir @@ "test"
let tempDir = ".temp"
let isLocalBuild = BuildServer.isLocalBuild
                && (Environment.environVarAsBoolOrDefault "PBITOOLS_IsLocalBuild" true)

// Pattern specifying assemblies to be tested using xUnit
let testAssemblies = outDir @@ "*Tests*.dll"

let (|HasCustomVersion|_|) = function
    | head :: _ -> let m = Regex.Match(head, @"<version:([0-9a-zA-Z\-\.\+]+)>")
                   if (m.Success) then Some (m.Groups.[1].Value)
                   else None
    | _ -> None

let mutable releaseNotesData = []
let mutable fileVersion = ""
let mutable releaseVersion = ""

// let stable = 
//     match releaseNotesData |> List.tryFind (fun r -> r.NugetVersion.Contains("-") |> not) with
//     | Some stable -> stable
//     | _ -> release

/// If 'PBITOOLS_PbiInstallDir' points to a valid PBI Desktop installation, set the MSBuild 'ReferencePath' property to that location
let pbiInstallDir =
    lazy ( match Environment.environVarOrNone "PBITOOLS_PbiInstallDir" with // PBIDesktop.exe might be in a sub-folder .. getting that folder here
           | Some path -> Directory.EnumerateFiles(path, "PBIDesktop.exe", SearchOption.AllDirectories)
                         |> Seq.tryHead
                         |> function
                            | Some p -> Some (p |> Path.getDirectory)
                            | _ -> None
           | None -> None )

let pbiBuildVersion =
    lazy ( let dir = match pbiInstallDir.Value with
                     | Some p -> p
                     | _ -> @"C:\Program Files\Microsoft Power BI Desktop\bin" // this path is hard-coded in the csproj, so fine to do the same here
           let pbiDesktopPath = Path.combine dir "PBIDesktop.exe"
           if pbiDesktopPath |> File.exists then
              pbiDesktopPath |> File.tryGetVersion
           else
              None )

let genCSAssemblyInfo (projectPath : string) =
    let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
    let folderName = System.IO.Path.GetDirectoryName(projectPath)
    let basePath = folderName @@ "Properties"
    let fileName = basePath @@ "AssemblyInfo.cs"
    let (release: ReleaseNotes.ReleaseNotes) = releaseNotesData |> List.head
    AssemblyInfoFile.createCSharp fileName
      [ AssemblyInfo.Title (projectName)
        AssemblyInfo.Product project
        AssemblyInfo.Company company
        AssemblyInfo.Copyright (sprintf "Copyright (C) Mathias Thierbach 2018-%i" (let today = DateTime.Today in today.Year)) // Avoids warning FS0052, see: https://github.com/fsharp/FAKE/issues/1803
        AssemblyInfo.Description summary
        AssemblyInfo.Version release.AssemblyVersion
        AssemblyInfo.FileVersion fileVersion
        AssemblyInfo.InformationalVersion releaseVersion
        AssemblyInfo.Metadata ("PBIBuildVersion", match pbiBuildVersion.Value with | Some v -> v | _ -> "" ) ]


// --------------------------------------------------------------------------------------
// TARGET IMPLEMENTATIONS
// --------------------------------------------------------------------------------------

// Generate assembly info files with the right version & up-to-date information
let assemblyInfo _ =
    !! "src/**/*.csproj"
    |> Seq.filter (fun s -> not <| s.Contains("PbiDownloader"))
    |> Seq.iter genCSAssemblyInfo


// --------------------------------------------------------------------------------------
// Clean build results

let clean _ =
    !! "src/*/bin"
    ++ "tests/*/bin"
    ++ "src/*/obj"
    ++ "tests/*/obj"
    ++ buildDir 
    ++ tempDir
    |> Shell.cleanDirs


// --------------------------------------------------------------------------------------
// Build library & test project

let zipSampleData _ =
    tempDir |> Directory.ensure
    
    !! "data/Samples/Adventure Works DW 2020/**/*.*"
    |> Zip.zip "data/Samples/Adventure Works DW 2020" (tempDir @@ "Adventure Works DW 2020.zip")

    !! "data/Samples/Adventure Works DW 2020 - TE/**/*.*"
    |> Zip.zip "data/Samples/Adventure Works DW 2020 - TE" (tempDir @@ "Adventure Works DW 2020 - TE.zip")


let buildTools _ =
    !! "tools/*/*.csproj"
    |> Seq.iter (fun path ->
        let proj = path |> FileInfo.ofPath
        let outPath = outDir @@ proj.Directory.Name

        proj.FullName
        |> DotNet.publish (fun args ->
            { args with
                OutputPath = Some outPath
                Configuration = DotNet.BuildConfiguration.Release
                MSBuildParams = { MSBuild.CliArguments.Create() with DisableInternalBinLog = true }
            })

        !! (outPath @@ "*.*")
        |> Zip.zip outPath (outDir @@ (sprintf "%s.zip" proj.Directory.Name))

    )


// Including 'Restore' target addresses issue: https://github.com/fsprojects/Paket/issues/2697
// Previously, msbuild would fail not being able to find **\obj\project.assets.json
let build _ =
    let msbuildProps = match pbiInstallDir.Value with
                       | Some dir -> Trace.logfn "Using assembly ReferencePath: %s" dir
                                     [ "ReferencePath", dir ]
                       | _ -> [ "_", "dummy" ]  // temp fix for https://github.com/fsprojects/FAKE/issues/2738

    !! "tests/**/*.csproj"
    |> MSBuild.runReleaseExt
      (fun args -> { args with DisableInternalBinLog = true })
      null
      msbuildProps
      "Restore;Rebuild"
    |> ignore


let ciBuild _ =
    !! "tests/**/*.csproj"
    -- "tests/**/PBI-Tools.Tests.csproj"
    -- "tests/**/PBI-Tools.IntegrationTests.csproj"
    |> Seq.iter (
        DotNet.build (fun args ->
        { args with Configuration = DotNet.BuildConfiguration.Release
                    MSBuildParams = { args.MSBuildParams with DisableInternalBinLog = true } })
    )


let publish _ = 
    let msbuildProps = match pbiInstallDir.Value with
                       | Some dir -> Trace.logfn "Using assembly ReferencePath: %s" dir
                                     [ "ReferencePath", dir ]
                       | _ -> []

    let setParams (tfm, rid, path) =
        fun (args : DotNet.PublishOptions) -> 
            { args with
                Runtime = Some rid
                Framework = Some tfm
                SelfContained = Some false
                //NoRestore = true
                OutputPath = Some path
                Configuration = DotNet.BuildConfiguration.Release
                MSBuildParams = { args.MSBuildParams with
                                       Properties = msbuildProps
                                       DisableInternalBinLog = true }
            }
    
    // Desktop build
    "src/PBI-Tools/PBI-Tools.csproj"
    |> DotNet.publish
        (setParams ("net472", "win10-x64", distFullDir)) 

    // Hack: Remove all libgit2sharp files
    (distFullDir @@ "lib")
    |> Directory.delete

    !! (distFullDir @@ "*.*")
    -- "**/pbi-tools.*"
    |> File.deleteAll

    [ "net8.0", distCoreDir
      "net9.0", distNetXDir ]
    |> Seq.iter (fun (tfm, dir) ->
        [ "win-x64",        "win-x64"
          "linux-x64",      "linux-x64"
          "linux-musl-x64", "alpine-x64" ]
        |> Seq.iter (fun (rid, path) ->
            "src/PBI-Tools.NETCore/PBI-Tools.NETCore.csproj"
            |> DotNet.publish 
                (setParams (tfm, rid, dir @@ path)) 
        )
    )


let sign _ =
    // distFullDir @@ "pbi-tools.exe"
    // distCoreDir @@ "win-x64" @@ "pbi-tools.core.exe"
    // distNetXDir @@ "win-x64" @@ "pbi-tools.net9.exe"
    // distDir/**/*.exe

    let ifl = distDir @@ "files.txt"

    !! (distDir @@ "**/*.exe")
    |> File.write false ifl

    let args = [|
        "azuresigntool"
        "sign"
        "-kvu"; Environment.environVarOrFail "PBITOOLS_KVU"
        "-kvt"; Environment.environVarOrFail "PBITOOLS_KVT"
        "-kvi"; Environment.environVarOrFail "PBITOOLS_KVI"
        "-kvs"; Environment.environVarOrFail "PBITOOLS_KVS"
        "-kvc"; Environment.environVarOrFail "PBITOOLS_KVC"
        "-du"; "https://pbi.tools"
        "-tr"; "http://timestamp.digicert.com"
        "-td"; "sha384"
        "-fd"; "sha384"
        "-ifl"; ifl
        "-v" |]

    let result = Process.shellExec {
            Program = "dotnet"
            CommandLine = String.Join(' ', args)
            Args = []
            WorkingDir = "."
        }

    // Check the result
    if result <> 0 then
        failwithf "Shell command failed with exit code %d" result


let pack _ =
    !! (distFullDir @@ "*.*")
    |> Zip.zip distFullDir (sprintf @"%s\pbi-tools.%s.zip" buildDir releaseVersion)

    distCoreDir
    |> Directory.EnumerateDirectories
    |> Seq.map (Path.GetFileName) 
    |> Seq.iter (fun dist ->
        !! (distCoreDir @@ dist @@ "*.*")
        |> Zip.zip (distCoreDir @@ dist) (sprintf @"%s\pbi-tools.core.%s_%s.zip" buildDir releaseVersion dist)
    )

    distNetXDir
    |> Directory.EnumerateDirectories
    |> Seq.map (Path.GetFileName) 
    |> Seq.iter (fun dist ->
        !! (distNetXDir @@ dist @@ "*.*")
        |> Zip.zip (distNetXDir @@ dist) (sprintf @"%s\pbi-tools.%s.%s_%s.zip" buildDir netXLabel releaseVersion dist)
    )


let test _ =
    if isLocalBuild then
        !! "tests/*/bin/Release/**/pbi-tools*tests.dll"
        -- "tests/*/bin/Release/**/*netcore.tests.dll"
        |> XUnit2.run (fun p ->
                                    { p with HtmlOutputPath = Some (testDir @@ "xunit.html")
                                             XmlOutputPath = Some (testDir @@ "xunit.xml")
                                             ToolPath = "packages/fake-tools/xunit.runner.console/tools/net472/xunit.console.exe" } )

    // https://fake.build/apidocs/v5/fake-dotnet-dotnet-testoptions.html
    "tests/PBI-Tools.NetCore.Tests/PBI-Tools.NetCore.Tests.csproj"
    |> DotNet.test (fun defaults ->
       { defaults with
           ResultsDirectory = Some "./.build/test"
           Configuration = DotNet.BuildConfiguration.Release
           ListTests = true
           Logger = Some "trx;LogFileName=TestOutput.NetCore.xml"
           MSBuildParams = { defaults.MSBuildParams with DisableInternalBinLog = true }
       })


let smokeTest _ =
    // Copy all *.pbix from /data folder to TEMP
    // Run 'pbi-tools extract' on all
    // Fail if error code is returned

    let dir = match Environment.environVarOrNone "PBITOOLS_TempDir" with
              | Some x -> x
              | None -> tempDir

    dir |> Directory.ensure

    !! "data/**/*.pbix"
    -- "data/external/**"
    |> Shell.copyFilesWithSubFolder dir

    // 'external' folder contains files with deeply nested folder structures,
    // likely to hit the Windows 260-character limit for paths
    // copying those without sub folders to keep extracted paths shorter
    !! "data/external/**/*.pbix"
    |> Shell.copyFiles dir

    !! (dir @@ "**/*.pbix")
    |> Seq.iter (fun path ->
        [ "extract"; path ]
        |> CreateProcess.fromRawCommand (distFullDir @@ "pbi-tools.exe")
        |> CreateProcess.ensureExitCode
        |> Proc.run
        |> ignore
    )


let usageDocs _ =
    [ (distFullDir @@ "pbi-tools.exe"), "./docs/usage.md"
      (distCoreDir @@ "win-x64" @@ "pbi-tools.core.exe"), "./docs/usage-core.md" ]
    |> Seq.iter (fun (command, output) ->
        [ "export-usage"; "-outPath"; output ]
        |> CreateProcess.fromRawCommand command
        |> CreateProcess.ensureExitCode
        |> Proc.run
        |> ignore
    )


let writeHeaders _ =
    !! "src/**/*.cs"
    ++ "tests/**/*.cs"
    -- "src/**/AssemblyInfo.cs"
    -- "src/**/obj/**/*.cs"
    -- "tests/**/obj/**/*.cs"
    |> Seq.iter (fun path ->
        Trace.logfn "Processing: %s" path
        use original = new StringReader(path |> File.ReadAllText)
        use file = path |> File.CreateText

        // loop through the lines of 'original'
        // skip comment lines
        // write header if not already written
        // write remaining lines

        let mutable line = original.ReadLine()
        while (line <> null && not <| line.StartsWith("/**") && (line.StartsWith('/') || line.StartsWith(" *") || line = String.Empty)) do
            // Skip comment lines and empty lines
            line <- original.ReadLine()

        file.WriteLine("/*")
        file.WriteLine(" * This file is part of the pbi-tools project <https://github.com/pbi-tools/pbi-tools>.")
        file.WriteLine(" * Copyright (C) 2018 Mathias Thierbach")
        file.WriteLine(" *")
        file.WriteLine(" * pbi-tools is free software: you can redistribute it and/or modify")
        file.WriteLine(" * it under the terms of the GNU Affero General Public License as published by")
        file.WriteLine(" * the Free Software Foundation, either version 3 of the License, or")
        file.WriteLine(" * (at your option) any later version.")
        file.WriteLine(" *")
        file.WriteLine(" * pbi-tools is distributed in the hope that it will be useful,")
        file.WriteLine(" * but WITHOUT ANY WARRANTY; without even the implied warranty of")
        file.WriteLine(" * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the")
        file.WriteLine(" * GNU Affero General Public License for more details.")
        file.WriteLine(" *")
        file.WriteLine(" * A copy of the GNU Affero General Public License is available in the LICENSE file,")
        file.WriteLine(" * and at <https://goto.pbi.tools/license>.")
        file.WriteLine(" */")
        file.WriteLine()

        while (line <> null) do
            if not <| (original.Peek() = -1 && line = String.Empty) then
                file.WriteLine(line)
            line <- original.ReadLine()
    )


let help _ =
    Trace.traceImportant "Please specify a target to run."
    Target.listAvailable()

// --------------------------------------------------------------------------------------

open Fake.Core.TargetOperators

let initTargets () =
    BuildServer.install [
        GitHubActions.Installer  // Adds support for GH Actions
    ]

    // Read additional information from the release notes document
    releaseNotesData <- 
        File.ReadAllLines "RELEASE_NOTES.md"
        |> ReleaseNotes.parseAll

    let release = releaseNotesData |> List.head
    let timestampString =
        let now = DateTime.UtcNow
        let ytd = now - DateTime(now.Year,1,1,0,0,0,DateTimeKind.Utc)
        String.Format("{0:yy}{1:ddd}.{0:HHmm}",now,ytd + TimeSpan.FromDays(1.))
    fileVersion <- sprintf "%i.%i.%s"
                    release.SemVer.Major
                    release.SemVer.Minor
                    timestampString
    releaseVersion <- match release.Notes with
                        | HasCustomVersion version -> version
                        | _ -> release.NugetVersion
    Trace.logfn "Release Version:\n%s" releaseVersion
    //Trace.logfn "Current Release:\n%O" release

    Target.create "AssemblyInfo" assemblyInfo
    Target.create "Clean" clean
    Target.create "ZipSampleData" zipSampleData
    Target.create "BuildTools" buildTools
    Target.create "Build" build
    Target.create "CI-Build" ciBuild
    Target.create "Publish" publish
    Target.create "Sign" sign
    Target.create "Pack" pack
    Target.create "Test" test
    Target.create "SmokeTest" smokeTest
    Target.create "UsageDocs" usageDocs
    Target.create "Help" help
    Target.create "WriteHeaders" writeHeaders

    "Clean"
    ==> "AssemblyInfo"
    ==> "ZipSampleData"
    ==> "BuildTools"
    ==> (if isLocalBuild then "Build" else "CI-Build")
    ==> "Test"
    ==> "Publish"
    ==> "Sign"
    ==> "UsageDocs"
    ==> "Pack"

    "Publish"
    ==> "SmokeTest"

//-----------------------------------------------------------------------------
// Target Start
//-----------------------------------------------------------------------------

[<EntryPoint>]
let main argv =
    argv
    |> Array.toList
    |> Context.FakeExecutionContext.Create false "build.fsx"
    |> Context.RuntimeContext.Fake
    |> Context.setExecutionContext
    initTargets ()
    Target.runOrDefaultWithArguments "Help"

    0 // return an integer exit code