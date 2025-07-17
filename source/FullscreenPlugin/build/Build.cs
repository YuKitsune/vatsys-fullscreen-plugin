using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Win32;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.Git;
using Nuke.Common.Tools.GitHub;
using Nuke.Common.Tools.GitVersion;
using Octokit;
using Serilog;

[GitHubActions(
    "Build",
    GitHubActionsImage.WindowsLatest,
    OnPullRequestBranches = ["main"],
    InvokedTargets = [nameof(Compile)]
)]
[GitHubActions(
    "Release",
    GitHubActionsImage.WindowsLatest,
    OnPushBranches = ["main"],
    ImportSecrets = [nameof(GithubToken)],
    InvokedTargets = [nameof(Release)],
    FetchDepth = 0,
    WritePermissions = [GitHubActionsPermissions.Contents]
)]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    
    AbsolutePath PluginProjectFile => RootDirectory / "source" / "FullscreenPlugin" / "FullscreenPlugin" / "FullscreenPlugin.csproj";
    AbsolutePath OutputDirectory => RootDirectory / ".build";
    AbsolutePath ZipFile => OutputDirectory / $"FullscreenPlugin.{GitVersion.FullSemVer}.zip";
    
    [CanBeNull] AbsolutePath VatSysInstallDirectory => Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Sawbe\vatSys", "Path", null).ToString();
    bool VatSysIsInstalled => !string.IsNullOrEmpty(VatSysInstallDirectory) && VatSysInstallDirectory.Exists();
    [CanBeNull] AbsolutePath DebugOutputDirectory => VatSysInstallDirectory / "bin" / "Plugins" / "FullscreenPlugin - Debug";

    [GitVersion]
    readonly GitVersion GitVersion;
    
    [GitRepository]
    readonly GitRepository gitRepository;
    
    GitHubClient gitHubClient;
    
    [Secret]
    [Parameter("Github Token")]
    readonly string GithubToken;
    
    Target Clean => _ => _
        .Executes(() =>
        {
            Log.Information("Cleaning {OutputDirectory}", OutputDirectory);
            OutputDirectory.CreateOrCleanDirectory();
            
            if (DebugOutputDirectory is not null)
            {
                Log.Information("Cleaning {debugOutputPath}", DebugOutputDirectory);
                OutputDirectory.CreateOrCleanDirectory();
            }
        });
    
    Target InstallVatSys => _ => _
        .OnlyWhenDynamic(() => !VatSysIsInstalled)
        .Executes(async () =>
        {
            var zipPath = RootDirectory / "vatsys.zip";
            var installDir = TemporaryDirectory / "vatSys";
            installDir.CreateDirectory();
            
            await HttpTasks.HttpDownloadFileAsync("https://vatsys.sawbe.com/downloads/vatSysSetup.zip", zipPath);
            zipPath.UncompressTo(installDir);
            
            var msiFile = installDir.GlobFiles("**/vatSysSetup.msi").FirstOrDefault();
            if (msiFile == null)
                throw new Exception("vatSysSetup.msi not found after extraction.");

            var msiExecFile = ToolPathResolver.GetPathExecutable("msiexec")
                ?? throw new Exception("msiexec not found in system PATH");
            
            var msiexec = ToolResolver.GetTool(msiExecFile);
            msiexec($"/a \"{msiFile}\" /qb TARGETDIR=\"{installDir}\"");
        });

    Target Compile => _ => _
        .DependsOn(Clean)
        .DependsOn(InstallVatSys)
        .Executes(() =>
        {
            Log.Information(
                "Building version {Version} with configuration {Configuration} to {OutputDirectory}",
                GitVersion,
                Configuration,
                OutputDirectory);

            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(PluginProjectFile)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(OutputDirectory)
                .SetVersion(GitVersion.MajorMinorPatch));
        });
    
    Target Package => _ => _
        .DependsOn(Compile)
        .Requires(() => Configuration == Configuration.Release)
        .Produces(OutputDirectory / "*.zip")
        .Executes(() =>
        {
            var readmeFile = RootDirectory / "README.md";

            readmeFile.CopyToDirectory(OutputDirectory, ExistsPolicy.FileOverwrite);
            
            Log.Information("Packaging {OutputDirectory} to {ZipPath}", OutputDirectory, ZipFile);
            OutputDirectory.ZipTo(ZipFile);
        });
    
    Target Install => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            if (DebugOutputDirectory is null)
            {
                Log.Error("Could not find the debug output path. Please ensure the registry key exists.");
                return;
            }

            DebugOutputDirectory.CreateOrCleanDirectory();
            OutputDirectory.CopyToDirectory(DebugOutputDirectory);

            Log.Information("Plugin installed to {PluginPath}", DebugOutputDirectory);
        });
    
    Target SetupGithubActor => _ => _
        .Executes(() =>
        {
            var actor = Environment.GetEnvironmentVariable("GITHUB_ACTOR");
            GitTasks.Git($"config --global user.name '{actor}'");
            GitTasks.Git($"config --global user.email '{actor}@github.com'");
            if (IsServerBuild)
            {
                GitTasks.Git($"remote set-url origin https://{actor}:{GithubToken}@github.com/{gitRepository.GetGitHubOwner()}/{gitRepository.GetGitHubName()}.git");
            }
        });

    Target TagRelease => _ => _
        .OnlyWhenDynamic(() => gitRepository.IsOnMainOrMasterBranch())
        .OnlyWhenDynamic(() => !string.IsNullOrWhiteSpace(GithubToken))
        .DependsOn(Package)
        .DependsOn(SetupGithubActor)
        .Executes(() =>
        {
            var version = GitVersion.MajorMinorPatch;
            
            // Delete the existing "latest" tag
            GitTasks.Git("tag -d latest");
            GitTasks.Git("push origin --tags");
            
            // Create a new tag for the current version and set "latest" to point to it
            GitTasks.Git($"tag {version}");
            GitTasks.Git($"tag latest");
            GitTasks.Git("push origin --tags");
        });

    Target SetupGitHubClient => _ => _
        .OnlyWhenDynamic(() => gitRepository.IsOnMainOrMasterBranch())
        .OnlyWhenDynamic(() => !string.IsNullOrWhiteSpace(GithubToken))
        .DependsOn(SetupGithubActor)
        .Executes(() =>
        {
            gitHubClient = new GitHubClient(new ProductHeaderValue("Nuke"));
            var tokenAuth = new Credentials(GithubToken);
            gitHubClient.Credentials = tokenAuth;
        });
    
    Target Release => _ => _
        .OnlyWhenDynamic(() => gitRepository.IsOnMainOrMasterBranch())
        .OnlyWhenDynamic(() => !string.IsNullOrWhiteSpace(GithubToken))
        .DependsOn(Package)
        .DependsOn(TagRelease)
        .DependsOn(SetupGitHubClient)
        .Executes(async () =>
        {
            var release = await gitHubClient.Repository.Release.Create(
                gitRepository.GetGitHubOwner(),
                gitRepository.GetGitHubName(),
                new NewRelease(GitVersion.MajorMinorPatch)
                {
                    Draft = true,
                    Name = gitRepository.IsOnMainOrMasterBranch()
                        ? $"{GitVersion.MajorMinorPatch}"
                        : $"{GitVersion.SemVer}",
                    TargetCommitish = GitVersion.Sha
                });
            
            Log.Information($"Release {release.Name} created");
            
            var fileName = $"Plugin.zip";
            var fileStream = File.OpenRead(ZipFile);
            await gitHubClient.Repository.Release.UploadAsset(
                release,
                new ReleaseAssetUpload(fileName, "application/zip", fileStream, TimeSpan.FromSeconds(5)));
            
            Log.Information($"{fileName} uploaded");
        });
}
