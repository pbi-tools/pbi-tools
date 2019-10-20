System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

// [x] clean
// [x] assemblyinfo
// [x] build
// [ ] test
// [ ] docs (gh-pages)
// [ ] package (nuget)
// [ ] release (myget, chocolatey)

#r @"packages/build/FAKE/tools/FakeLib.dll"
#r "System.IO.Compression.FileSystem"

open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open System
open System.IO

// --------------------------------------------------------------------------------------
// START TODO: Provide project-specific details below
// --------------------------------------------------------------------------------------

// Information about the project are used
//  - for version and project name in generated AssemblyInfo file
//  - by the generated NuGet package
//  - to run tests and to publish documentation on GitHub gh-pages
//  - for documentation, you also need to edit info in "docs/tools/generate.fsx"

// The name of the project
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "pbi-tools"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "The Power BI developer's toolkit."

// Longer description of the project
// (used as a description for NuGet package; line breaks are automatically cleaned up)
let description = "A command-line tool to work with Power BI Desktop files. Enables change tracking for PBIx files in source control as well as generating those files programmatically."

// List of author names (for NuGet package)
let authors = [ "Mathias Thierbach" ]

// Tags for your project (for NuGet package)
let tags = "powerbi, pbix, source-control, automation"

// File system information
let solutionFile  = "PBI-TOOLS.sln"
let distProject = "src/*/PBI-Tools.csproj"

// Pattern specifying assemblies to be tested using xUnit
let testAssemblies = "tests/**/bin/**/*Tests*.dll"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "pbi-tools"
let gitHome = "https://github.com/" + gitOwner

// The name of the project on GitHub
let gitName = "pbi-tools"

// The url for the raw files hosted
let gitRaw = environVarOrDefault "gitRaw" ("https://raw.github.com/" + gitOwner)


// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps
// --------------------------------------------------------------------------------------

let buildDir = ".build"
let outDir = buildDir @@ "out"
let distDir = buildDir @@ "dist"
let tempDir = ".temp"

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

System.Net.ServicePointManager.SecurityProtocol <- unbox 192 ||| unbox 768 ||| unbox 3072 ||| unbox 48

// Read additional information from the release notes document
let releaseNotesData = 
    File.ReadAllLines "RELEASE_NOTES.md"
    |> parseAllReleaseNotes

let release = List.head releaseNotesData
let assemblyVersion = sprintf "%i.%i.0.0" release.SemVer.Major release.SemVer.Minor
let timestampString =
    let now = DateTime.UtcNow
    let ytd = now - DateTime(now.Year,1,1,0,0,0,DateTimeKind.Utc)
    String.Format("{0:yy}{1:ddd}.{0:HHmm}",now,ytd)
let fileVersion = sprintf "%i.%i.%s"
                   release.SemVer.Major
                   release.SemVer.Minor
                   timestampString

let stable = 
    match releaseNotesData |> List.tryFind (fun r -> r.NugetVersion.Contains("-") |> not) with
    | Some stable -> stable
    | _ -> release


let genCSAssemblyInfo (projectPath) =
    let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
    let folderName = System.IO.Path.GetDirectoryName(projectPath)
    let basePath = folderName @@ "Properties"
    let fileName = basePath @@ "AssemblyInfo.cs"
    CreateCSharpAssemblyInfo fileName
      [ Attribute.Title (projectName)
        Attribute.Product project
        Attribute.Company (authors |> String.concat ", ")
        Attribute.Copyright (sprintf "Copyright \u00A9 Mathias Thierbach %i" 2018)
        Attribute.Description summary
        Attribute.Version release.AssemblyVersion
        Attribute.FileVersion fileVersion
        Attribute.InformationalVersion release.NugetVersion ]

// Generate assembly info files with the right version & up-to-date information
Target "AssemblyInfo" (fun _ ->
    let csProjs = !! "src/**/*.csproj" |> Seq.filter (fun s -> not <| s.Contains("preview"))
    csProjs |> Seq.iter genCSAssemblyInfo
)



// --------------------------------------------------------------------------------------
// Clean build results

Target "Clean" (fun _ ->
    !! "src/**/bin"
    ++ "tests/**/bin"
    ++ "src/**/obj"
    ++ "tests/**/obj"
    ++ buildDir 
    ++ tempDir
    |> CleanDirs

    !! "**/obj/**/*.nuspec"
    |> DeleteFiles
)


// --------------------------------------------------------------------------------------
// Build library & test project

// Including 'Restore' target addresses issue: https://github.com/fsprojects/Paket/issues/2697
// Previously, msbuild would fail not being able to find **\obj\project.assets.json
Target "Build" (fun _ ->
    // !! distProject
    !! solutionFile
    |> MSBuildReleaseExt outDir [
            "VisualStudioVersion" , "15.0"
            "ToolsVersion"        , "15.0"
            // "SolutionDir"         , __SOURCE_DIRECTORY__
    ] "Restore;Rebuild"
    |> ignore

    // Could not get Fody to do its thing unless when building the entire solution, so we're grabbing the dist files here explicitly
    !! (outDir @@ "pbi-tools.*")
    -- (outDir @@ "*test*")
    -- (outDir @@ "*.runtimeconfig.*")
    |> CopyFiles distDir
)

Target "Help" DoNothing

// --------------------------------------------------------------------------------------

"Clean"
  ==> "AssemblyInfo"
  ==> "Build"

// --------------------------------------------------------------------------------------
// Show help by default. Invoke 'build <Target>' to override

RunTargetOrDefault "Help"
