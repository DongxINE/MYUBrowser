param(
    [string]$PublishDir,
    # 本地发布产物输出目录；不要提交真实机器路径到仓库。
    # 可通过环境变量 MYU_RELEASE_DIR 覆盖，默认写到项目下的 releases 文件夹。
    [string]$ReleaseDir = $(if ($env:MYU_RELEASE_DIR) { $env:MYU_RELEASE_DIR } else { Join-Path $PSScriptRoot "releases" })
)

$cleanPublishDir = $PublishDir.Replace("'", "").Replace('"', "").TrimEnd('\')

if (Test-Path $cleanPublishDir) {
    $cleanPublishDir = (Get-Item $cleanPublishDir).FullName
}

New-Item -ItemType Directory -Force -Path $ReleaseDir | Out-Null

$releaseFile = Join-Path $ReleaseDir "RELEASES"
$nextVer = "1.0.0"

if (Test-Path $releaseFile) {
    $lastLine = Get-Content $releaseFile | Select-Object -Last 1
    if ($lastLine -match 'MYUBrowser-([\d\.]+)-full\.nupkg') {
        $currentVer = [version]$Matches[1]
        $nextVer = New-Object version($currentVer.Major, $currentVer.Minor, ($currentVer.Build + 1))
    }
}

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " 本次自动递增打包版本: $nextVer" -ForegroundColor Green
Write-Host " 打包源目录: $cleanPublishDir" -ForegroundColor Yellow
Write-Host " 发布输出目录: $ReleaseDir" -ForegroundColor Yellow
Write-Host "==================================================" -ForegroundColor Cyan

vpk pack --packId "MYUBrowser" --packVersion $nextVer.ToString() --packDir "$cleanPublishDir" --mainExe "MYUBrowser.exe" --outputDir $ReleaseDir

# 上传到 GitHub Releases（需要先 gh auth login 或设置 GITHUB_TOKEN）：
#   vpk upload github --repoUrl https://github.com/your-name/MYU-Browser --publish --releaseName "MYU Browser $nextVer" --tag "v$nextVer" --token $env:GITHUB_TOKEN
