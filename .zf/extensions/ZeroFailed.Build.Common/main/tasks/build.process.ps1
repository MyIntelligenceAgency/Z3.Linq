# <copyright file="build.process.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

# Top-level build process control flags
$SkipInit = $false
$SkipVersion = $false
$SkipBuild = $false
$SkipAnalysis = $false
$SkipTest = $false
$SkipPackage = $false
$SkipPublish = $false

# Define a logical build process that extensions can attach to
task PreInit
task InitCore
task PostInit
task Init -If {!$SkipInit} PreInit,InitCore,PostInit

task PreVersion
task VersionCore
task PostVersion
task Version -If {!$SkipVersion} Init,PreVersion,VersionCore,PostVersion

task PreBuild
task BuildCore
task PostBuild
task Build -If {!$SkipBuild} Init,Version,PreBuild,BuildCore,PostBuild

task PreAnalysis
task AnalysisCore
task PostAnalysis
task Analysis -If {!$SkipAnalysis} Init,Version,PreAnalysis,AnalysisCore,PostAnalysis

task PreTest
task TestCore
task PostTest
task Test -If {!$SkipTest} Init,PreTest,TestCore,PostTest

task PrePackage
task PackageCore
task PostPackage
task Package -If {!$SkipPackage} Init,Version,PrePackage,PackageCore,PostPackage

task PrePublish
task PublishCore
task PostPublish
task Publish -If {!$SkipPublish} Init,Version,PrePublish,PublishCore,PostPublish

task RunFirst
task RunLast

# Synopsis: Run the full build process without publishing outputs.
task FullBuild RunFirst,
                Init,
                Version,
                Build,
                Test,
                Analysis,
                Package,
                RunLast

# Synopsis: Run the full build process including publishing outputs.
task FullBuildAndPublish RunFirst,
                            Init,
                            Version,
                            Build,
                            Test,
                            Analysis,
                            Package,
                            Publish,
                            RunLast
