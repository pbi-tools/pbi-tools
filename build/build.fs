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
let distNet6Dir = distDir @@ "net6"
let testDir = buildDir @@ "test"
let tempDir = ".temp"

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
        AssemblyInfo.Copyright (sprintf "Copyright \u00A9 Mathias Thierbach 2018-%i" (let today = DateTime.Today in today.Year)) // Avoids warning FS0052, see: https://github.com/fsharp/FAKE/issues/1803
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
    // let setParams (defaults:MSBuildParams) =
    //     { defaults with
    //         MaxCpuCount = None 
    //     }

    !! solutionFile
    |> MSBuild.runReleaseExt id null msbuildProps "Restore;Rebuild"
    |> ignore


let publish _ = 
    let msbuildProps = match pbiInstallDir.Value with
                       | Some dir -> Trace.logfn "Using assembly ReferencePath: %s" dir
                                     [ "ReferencePath", dir ]
                       | _ -> []

    let setParams (rid, path) =
        fun (args : DotNet.PublishOptions) -> 
            { args with
                Runtime = Some rid
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
        (setParams ("win10-x64", distFullDir)) 

    // Hack: Remove all libgit2sharp files
    (distFullDir @@ "lib")
    |> Directory.delete

    !! (distFullDir @@ "*.*")
    -- "**/pbi-tools.*"
    |> File.deleteAll

    
    // Core build
    [ "win-x64",        "win-x64"
      "linux-x64",      "linux-x64"
      "linux-musl-x64", "alpine-x64" ]
    |> Seq.iter (fun (rid, path) ->
        "src/PBI-Tools.NETCore/PBI-Tools.NETCore.csproj"
        |> DotNet.publish 
            (setParams (rid, distCoreDir @@ path)) 
    )

    // Net6 build
    [ "win10-x64",      "win-x64"
      "linux-x64",      "linux-x64"
      "linux-musl-x64", "alpine-x64" ]
    |> Seq.iter (fun (rid, path) ->
        "src/PBI-Tools.NET6/PBI-Tools.NET6.csproj"
        |> DotNet.publish 
            (setParams (rid, distNet6Dir @@ path)) 
    )


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

    distNet6Dir
    |> Directory.EnumerateDirectories
    |> Seq.map (Path.GetFileName) 
    |> Seq.iter (fun dist ->
        !! (distNet6Dir @@ dist @@ "*.*")
        |> Zip.zip (distNet6Dir @@ dist) (sprintf @"%s\pbi-tools.net6.%s_%s.zip" buildDir releaseVersion dist)
    )


let test _ =
    !! "tests/*/bin/Release/**/pbi-tools*tests.dll"
    -- "tests/*/bin/Release/**/*netcore.tests.dll"
    -- "tests/*/bin/Release/**/*net6.tests.dll"
    |> XUnit2.run (fun p -> { p with HtmlOutputPath = Some (testDir @@ "xunit.html")
                                     XmlOutputPath = Some (testDir @@ "xunit.xml")
                                     ToolPath = "packages/fake-tools/xunit.runner.console/tools/net472/xunit.console.exe" } )
    // TODO Does XUnit2.run fail silently??

    // https://fake.build/apidocs/v5/fake-dotnet-dotnet-testoptions.html
    "tests/PBI-Tools.NetCore.Tests/PBI-Tools.NetCore.Tests.csproj"
    |> DotNet.test (fun defaults ->
       { defaults with
           ResultsDirectory = Some "./.build/test"
           Configuration = DotNet.BuildConfiguration.Release
           ListTests = true
           Logger = Some "trx;LogFileName=TestOutput.NetCore.xml"
       })

    "tests/PBI-Tools.Net6.Tests/PBI-Tools.Net6.Tests.csproj"
    |> DotNet.test (fun defaults ->
       { defaults with
           ResultsDirectory = Some "./.build/test"
           Configuration = DotNet.BuildConfiguration.Release
           ListTests = true
           Logger = Some "trx;LogFileName=TestOutput.Net7.xml"
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


let help _ =
    Trace.traceImportant "Please specify a target to run."
    Target.listAvailable()

// --------------------------------------------------------------------------------------

open Fake.Core.TargetOperators

let initTargets () =
    BuildServer.install [
        TeamFoundation.Installer  // Adds support for Azure DevOps
    ]

    //System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

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
    Target.create "Publish" publish
    Target.create "Pack" pack
    Target.create "Test" test
    Target.create "SmokeTest" smokeTest
    Target.create "UsageDocs" usageDocs
    Target.create "Help" help

    "Clean"
    ==> "AssemblyInfo"
    ==> "ZipSampleData"
    ==> "BuildTools"
    ==> "Build"
    ==> "Test"

    "Publish"
    ==> "SmokeTest"

    "Build"
    ==> "Publish"

    "Publish"
    ==> "Test"
    ==> "UsageDocs"
    ==> "Pack"


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