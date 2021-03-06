// --------------------------------------------------------------------------------------
// FAKE build script 
// --------------------------------------------------------------------------------------

#load ".fake/build.fsx/intellisense.fsx"


#if !FAKE
    #r "netstandard"
#endif
open Fake.Core
open Fake.Tools
open Fake.DotNet
open Fake.BuildServer
open Fake.IO
open Fake.DotNet.NuGet
open Fake.Core.TargetOperators
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open System


// --------------------------------------------------------------------------------------
// Information about the project to be used at NuGet and in AssemblyInfo files
// --------------------------------------------------------------------------------------

let project = "Deedle"
let authors = ["BlueMountain Capital";"FsLab"]
let summary = "Easy to use .NET library for data manipulation and scientific programming"
let description = """
  Deedle implements an efficient and robust frame and series data structures for 
  manipulating with structured data. It supports handling of missing values, 
  aggregations, grouping, joining, statistical functions and more. For frames and 
  series with ordered indices (such as time series), automatic alignment is also 
  available. """
let tags = "F# fsharp deedle dataframe series statistics data science"

let rpluginProject = "Deedle.RPlugin"
let rpluginSummary = "Easy to use .NET library for data manipulation with R project integration"
let rpluginDescription = """
  This package installs core Deedle package, together with an R type provider plugin 
  which makes it possible to pass data frames and time series between R and Deedle"""
let rpluginTags = "R RProvider"

let deedleExcelProject = "Deedle.Excel"
let deedleExcelSummary = "Deedle integration with Excel"
let deedleExcelDescription = """
  This package installs the core Deedle package, NetOffice.Excel, and a Deedle extension
  which makes it possible to send Deedle Frames to Excel."""
let deedleExcelTags = "Excel"

let gitHome = "https://github.com/fslaborg"
let gitName = "Deedle"

let bindir = "./bin"
let docsDir = "./docs"


Environment.CurrentDirectory <- __SOURCE_DIRECTORY__


BuildServer.install [
    AppVeyor.Installer
    Travis.Installer
]

let desiredSdkVersion = (DotNet.getSDKVersionFromGlobalJson ())
let mutable sdkPath = None
let getSdkPath() = (defaultArg sdkPath "dotnet")
let installed =
  try
    DotNet.getVersion id <> null
  with _ -> false

printfn "Desired .NET SDK version = %s" desiredSdkVersion
printfn "DotNetCli.isInstalled() = %b" installed

let getPathForSdkVersion (sdkVersion) =
  DotNet.install (fun v -> { v with Version = DotNet.Version sdkVersion }) (DotNet.Options.Create ())
  |> fun o -> o.DotNetCliPath

if installed then
    let installedSdkVersion = DotNet.getVersion id
    printfn "The installed default .NET SDK version reported by FAKE's 'DotNetCli.getVersion()' is %s" installedSdkVersion
    if installedSdkVersion <> desiredSdkVersion then
        match Environment.environVar "CI" with
        | null ->
            if installedSdkVersion > desiredSdkVersion then
                printfn "*** You have .NET SDK version '%s' installed, assuming it is compatible with version '%s'" installedSdkVersion desiredSdkVersion
            else
                printfn "*** You have .NET SDK version '%s' installed, we expect at least version '%s'" installedSdkVersion desiredSdkVersion
        | _ ->
            printfn "*** The .NET SDK version '%s' will be installed (despite the fact that version '%s' is already installed) because we want precisely that version in CI" desiredSdkVersion installedSdkVersion
            sdkPath <- Some (getPathForSdkVersion desiredSdkVersion)
    else
        sdkPath <- Some (getPathForSdkVersion installedSdkVersion)
else
    printfn "*** The .NET SDK version '%s' will be installed (no other version was found by FAKE helpers)" desiredSdkVersion
    sdkPath <- Some (getPathForSdkVersion desiredSdkVersion)

// Read release notes & version info from RELEASE_NOTES.md
let release = ReleaseNotes.load "RELEASE_NOTES.md" 

// Generate assembly info files with the right version & up-to-date information
Target.create "AssemblyInfo" (fun _ ->
  AssemblyInfoFile.createFSharp "src/Deedle/Common/AssemblyInfo.fs"
      [ AssemblyInfo.Title project
        AssemblyInfo.Product project
        AssemblyInfo.Description summary
        AssemblyInfo.Version release.AssemblyVersion
        AssemblyInfo.InformationalVersion release.NugetVersion
        AssemblyInfo.FileVersion release.AssemblyVersion] 

  AssemblyInfoFile.createFSharp "src/Deedle.RProvider.Plugin/AssemblyInfo.fs"
      [ AssemblyInfo.Title rpluginProject
        AssemblyInfo.Product rpluginProject
        AssemblyInfo.Description rpluginSummary
        AssemblyInfo.Version release.AssemblyVersion
        AssemblyInfo.InformationalVersion release.NugetVersion
        AssemblyInfo.FileVersion release.AssemblyVersion]

  AssemblyInfoFile.createFSharp "src/Deedle.Excel/AssemblyInfo.fs"
      [ AssemblyInfo.Title deedleExcelProject
        AssemblyInfo.Product deedleExcelProject
        AssemblyInfo.Description deedleExcelSummary
        AssemblyInfo.Version release.AssemblyVersion
        AssemblyInfo.InformationalVersion release.NugetVersion
        AssemblyInfo.FileVersion release.AssemblyVersion]          
)

// --------------------------------------------------------------------------------------
// Clean build results

Target.create "Clean" ( fun _ ->
    // have to clean netcore output directories because they corrupt the full-framework outputs
    seq {
        yield bindir
        yield! !!"**/bin"
        yield! !!"**/obj"
    } |> Shell.cleanDirs
)

Target.create "CleanDocs" (fun _ ->
    Shell.cleanDirs ["docs/output"]
)

// --------------------------------------------------------------------------------------
// Build library & test project

let testProjs = 
    [
      "tests/Deedle.Tests/Deedle.Tests.fsproj" 
      "tests/Deedle.CSharp.Tests/Deedle.CSharp.Tests.csproj" 
      "tests/Deedle.Documentation.Tests/Deedle.Documentation.Tests.fsproj"
      "tests/Deedle.PerfTests/Deedle.PerfTests.fsproj"
      // "tests/Deedle.RPlugin.Tests/Deedle.RPlugin.Tests.fsproj"  
    ]

let testCoreProjs = 
    [ "tests/Deedle.Tests/Deedle.Tests.fsproj" 
      "tests/Deedle.CSharp.Tests/Deedle.CSharp.Tests.csproj" 
      "tests/Deedle.Documentation.Tests/Deedle.Documentation.Tests.fsproj"
      "tests/Deedle.PerfTests/Deedle.PerfTests.fsproj" ]      

let buildProjs =
    [ "src/Deedle/Deedle.fsproj"
      "src/Deedle.RProvider.Plugin/Deedle.RProvider.Plugin.fsproj"
      "src/Deedle.Excel/Deedle.Excel.fsproj" ]

let buildCoreProjs =
    [ "src/Deedle/Deedle.fsproj"    
      "src/Deedle.Excel/Deedle.Excel.fsproj" ]

let setSdkPathAndVerbose (c: DotNet.Options) =
  { c with
      DotNetCliPath = getSdkPath ()
      CustomParams = Some "/v:n" }

Target.create "Build" ( fun _ ->
  Environment.setEnvironVar "GenerateDocumentationFile" "true"
  for proj in buildProjs do  
    DotNet.build (fun opts -> { opts with Common = { opts.Common with DotNetCliPath = getSdkPath ()
                                                                      CustomParams = Some "/v:n /p:SourceLinkCreate=true" }
                                          Configuration = DotNet.BuildConfiguration.Release }) proj )


Target.create "BuildCore" ( fun _ ->
  Environment.setEnvironVar "GenerateDocumentationFile" "true"
  for proj in buildCoreProjs do  
    DotNet.build (fun opts -> { opts with Common = { opts.Common with DotNetCliPath = getSdkPath ()
                                                                      CustomParams = Some "/v:n /p:SourceLinkCreate=true" }
                                          Configuration = DotNet.BuildConfiguration.Release }) proj )

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner & kill test runner when complete

Target.create "BuildTests" (fun _ ->
  for proj in testProjs do
    DotNet.build (fun opts -> { opts with Common = setSdkPathAndVerbose opts.Common
                                          Configuration = DotNet.BuildConfiguration.Release }) proj )

Target.create "RunTests" (fun _ ->
  for proj in testProjs do     
    DotNet.test (fun opts -> { opts with  Configuration = DotNet.BuildConfiguration.Release
                                          Common = setSdkPathAndVerbose opts.Common }) proj )

Target.create "BuildCoreTests" (fun _ ->
  for proj in testCoreProjs do
    DotNet.build (fun opts -> { opts with Common = setSdkPathAndVerbose opts.Common
                                          Configuration = DotNet.BuildConfiguration.Release }) proj )

Target.create "RunCoreTests" (fun _ ->
  for proj in testProjs do     
    DotNet.test (fun opts -> { opts with  Configuration = DotNet.BuildConfiguration.Release
                                          Common = setSdkPathAndVerbose opts.Common }) proj )

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target.create "NuGet" (fun _ ->
    // Format the description to fit on a single line (remove \r\n and double-spaces)
    let description = description.Replace("\r", "").Replace("\n", "").Replace("  ", " ")
    let rpluginDescription = rpluginDescription.Replace("\r", "").Replace("\n", "").Replace("  ", " ")
    let releaseNotes = release.Notes |> String.concat "\n"
    let nugetExe = "packages" </> "build" </> "NuGet.CommandLine" </> "tools" </> "NuGet.exe"

    NuGet.NuGetPack (fun p -> 
        { p with   
            ToolPath = nugetExe
            Authors = authors
            Project = project
            Summary = summary
            Description = description
            Version = release.NugetVersion
            ReleaseNotes = releaseNotes
            Tags = tags
            OutputPath = "bin"
            AccessKey = Environment.environVarOrDefault "nugetkey" ""
            Publish = Environment.hasEnvironVar "nugetkey" })
        ("nuget/Deedle.nuspec")
    NuGet.NuGetPack (fun p -> 
        { p with   
            ToolPath = nugetExe
            Authors = authors
            Project = rpluginProject
            Summary = rpluginSummary
            Description = description + "\n\n" + rpluginDescription
            Version = release.NugetVersion
            ReleaseNotes = releaseNotes
            Tags = tags + " " + rpluginTags
            OutputPath = "bin"
            Dependencies = 
              [ "Deedle", release.NugetVersion
                "R.NET.Community", NuGet.GetPackageVersion "packages" "R.NET.Community"
                "R.NET.Community.FSharp", NuGet.GetPackageVersion "packages" "R.NET.Community.FSharp"
                "RProvider", NuGet.GetPackageVersion "packages" "RProvider" ]
            AccessKey = Environment.environVarOrDefault "nugetkey" ""
            Publish = Environment.hasEnvironVar "nugetkey" })
        ("nuget/Deedle.RPlugin.nuspec")
    NuGet.NuGetPack (fun p -> 
        { p with   
            Authors = authors
            Project = deedleExcelProject
            Summary = deedleExcelSummary
            Description = description + "\n\n" + deedleExcelDescription
            Version = release.NugetVersion
            ReleaseNotes = releaseNotes
            Tags = tags + " " + deedleExcelTags
            OutputPath = "bin"    
            Dependencies = 
              [ "Deedle", release.NugetVersion
                "NetOffice.Core", NuGet.GetPackageVersion "packages" "NetOffice.Core"
                "NetOffice.Excel", NuGet.GetPackageVersion "packages" "NetOffice.Core" ]                    
            AccessKey = Environment.environVarOrDefault "nugetkey" ""
            Publish = Environment.hasEnvironVar "nugetkey" })
        ("nuget/Deedle.Excel.nuspec")
)

// --------------------------------------------------------------------------------------
// Generate the documentation

Target.create "GenerateDocs" (fun _ ->
  let (exitCode, messages) = Fsi.exec (fun p -> { p with WorkingDirectory="docs/tools"; Define="RELEASE"; }) "generate.fsx" []
  if exitCode = 0 then () else 
    failwith (messages |> String.concat Environment.NewLine)
)
// --------------------------------------------------------------------------------------
// Release Scripts

Target.create "ReleaseDocs" (fun _ ->
    Git.Repository.clone "" (gitHome + "/" + gitName + ".git") "temp/gh-pages"
    Git.Branches.checkoutBranch "temp/gh-pages" "gh-pages"
    Shell.copyRecursive "docs/output" "temp/gh-pages" true |> printfn "%A"
    Git.CommandHelper.runSimpleGitCommand "temp/gh-pages" "add ." |> printfn "%s"
    let cmd = sprintf """commit -a -m "Update generated documentation for version %s""" release.NugetVersion
    Git.CommandHelper.runSimpleGitCommand "temp/gh-pages" cmd |> printfn "%s"
    Git.Branches.push "temp/gh-pages"
)

Target.create "ReleaseBinaries" (fun _ ->
    Git.Repository.clone "" (gitHome + "/" + gitName + ".git") "temp/release"
    Git.Branches.checkoutBranch "temp/release" "release"
    
    // Delete old files and copy in new files
    !! "temp/release/*" |> File.deleteAll
    "temp/release/bin" |> Shell.cleanDir
    Shell.copyRecursive "bin" "temp/release/bin" true |> printfn "%A"
    !! "temp/release/bin/*.nupkg" |> File.deleteAll
    "temp/release/bin/Deedle.fsx" |> Shell.moveFile "temp/release"
    "temp/release/bin/RProvider.fsx" |> Shell.moveFile "temp/release"

    Git.CommandHelper.runSimpleGitCommand "temp/release" "add bin/*" |> printfn "%s"
    let cmd = sprintf """commit -a -m "Update binaries for version %s""" release.NugetVersion
    Git.CommandHelper.runSimpleGitCommand "temp/release" cmd |> printfn "%s"
    Git.Branches.push "temp/release"
)

Target.create "TagRelease" (fun _ ->
    // Concatenate notes & create a tag in the local repository
    let notes = (String.concat " " release.Notes).Replace("\n", ";").Replace("\r", "")
    let tagName = "v" + release.NugetVersion
    let cmd = sprintf """tag -a %s -m "%s" """ tagName notes
    Git.CommandHelper.runSimpleGitCommand "." cmd |> printfn "%s"

    // Find the main remote (fslaborg GitHub)
    let _, remotes, _ = Git.CommandHelper.runGitCommand "." "remote -v"
    let main = remotes |> Seq.find (fun s -> s.Contains("(push)") && s.Contains("fslaborg/Deedle"))
    let remoteName = main.Split('\t').[0]
    Git.Branches.pushTag "." remoteName tagName
)

Target.create "Release" ignore

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target.create "All" ignore
Target.create "AllCore" ignore

"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "All" 

"AssemblyInfo"
  ==> "BuildCore"
  ==> "AllCore"

"BuildTests" ==> "All"
"RunTests" ==> "All"
"BuildCoreTests" ==> "AllCore"
"RunCoreTests" ==> "AllCore"

"All" ==> "NuGet" ==> "Release"
"All" 
  ==> "CleanDocs"
  ==> "GenerateDocs"
  ==> "ReleaseDocs"
  ==> "ReleaseBinaries"
  ==> "TagRelease"
  ==> "Release"

Target.runOrDefault "AllCore"
