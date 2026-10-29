# Number Road: すべてのテストを実行する
#   powershell -ExecutionPolicy Bypass -File tools/run_tests.ps1 [-Only EditMode|PlayMode|Tools|Blender]
#  - Tools   : Python ツールの単体テスト（ビルド検査・ワークフロー・モデルの登録もれ）
#  - Blender : Blender のモデル生成スクリプトの単体テスト（部位・色・向き・大きさ・FBX 出力）
#  - EditMode: Unity の単体テスト（ゲートの計算・ステージ生成・バランス・隊列・保存）
#  - PlayMode: Unity の総合テスト（起動して自動操作で進む・一時停止）
param(
    [string]$Only = "",
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe",
    [string]$Blender = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$out = Join-Path $root "build-tmp"
New-Item -ItemType Directory -Force $out | Out-Null
$failed = @()

function Want($name) { return ($Only -eq "") -or ($Only -eq $name) }
function Quote([string]$s) { if ($s -match '\s') { return '"' + $s + '"' } return $s }

if (Want "Tools") {
    Write-Host "== Python tools"
    python -m unittest discover -s (Join-Path $root "tools/tests")
    if ($LASTEXITCODE -ne 0) { $failed += "Tools" }
}

if (Want "Blender") {
    Write-Host "== Blender models"
    & $Blender -b --factory-startup --python-exit-code 1 --python (Join-Path $root "blender/tests/test_models.py")
    if ($LASTEXITCODE -ne 0) { $failed += "Blender" }
}

foreach ($mode in @("EditMode", "PlayMode")) {
    if (-not (Want $mode)) { continue }
    Write-Host "== Unity $mode"
    $xml = Join-Path $out "results-$mode.xml"
    $log = Join-Path $out "test-$mode.log"
    if (Test-Path $xml) { Remove-Item $xml }
    $unityArgs = @("-batchmode", "-nographics", "-projectPath", (Join-Path $root "NumberRoad"), "-runTests", "-testPlatform", $mode,
        "-testResults", $xml, "-logFile", $log)
    $p = Start-Process -FilePath $Unity -Wait -PassThru -NoNewWindow -ArgumentList ($unityArgs | ForEach-Object { Quote $_ })
    if (-not (Test-Path $xml)) { $failed += $mode; Write-Host "結果ファイルがありません。ログ: $log"; continue }
    # 結果は UTF-8。PowerShell 5.1 の既定の文字コードで読むとこわれるので明示する
    [xml]$r = Get-Content -Raw -Encoding UTF8 $xml
    $run = $r."test-run"
    Write-Host ("  {0}: total={1} passed={2} failed={3}" -f $mode, $run.total, $run.passed, $run.failed)
    foreach ($tc in $r.SelectNodes("//test-case[@result='Failed']")) {
        Write-Host ("  FAIL {0}`n       {1}" -f $tc.fullname, $tc.failure.message.InnerText.Trim())
    }
    if ($p.ExitCode -ne 0) { $failed += $mode }
}

if ($failed.Count -gt 0) {
    Write-Host ("失敗: " + ($failed -join ", ")) -ForegroundColor Red
    exit 1
}
Write-Host "すべてのテストに合格しました" -ForegroundColor Green
