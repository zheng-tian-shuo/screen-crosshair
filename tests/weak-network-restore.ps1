# Run after build/build.ps1. Mock cmdlets ensure this test never changes networking.
$ErrorActionPreference = 'Stop'
$buildDir = Join-Path (Split-Path $PSScriptRoot -Parent) 'build'
$exe = Get-ChildItem -Path $buildDir -Filter '*.exe' |
    Where-Object { $_.Name -like '* v6.*.exe' } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if (-not $exe) { throw "找不到构建产物：$buildDir\* v6.*.exe" }
$assembly = [Reflection.Assembly]::LoadFile($exe.FullName)
$type = $assembly.GetType('ScreenCrosshair.WeakNetworkOps')
$builder = $type.GetMethod('BuildRestoreScript', [Reflection.BindingFlags]'Static,NonPublic')
$policyName = $type.GetMethod('PolicyName', [Reflection.BindingFlags]'Static,NonPublic')

$firstPolicy = $policyName.Invoke($null, @('Game.exe'))
$secondPolicy = $policyName.Invoke($null, @('game.EXE'))
if ($firstPolicy -ne $secondPolicy -or $firstPolicy -notmatch '^ScreenCrosshairWeak_[0-9A-F]{12}$') {
    throw 'Policy name must be stable before Apply starts'
}
Write-Host 'PASS stable-policy-name'

function Get-NetQosPolicy {
    [CmdletBinding()] param($PolicyStore)
    if ($PolicyStore -ne 'ActiveStore') { throw 'Wrong query store' }
    if ($script:mode -eq 'query-fails') { throw 'Query failed' }
    if ($script:exists) { [pscustomobject]@{Name='ScreenCrosshairWeak_test'} }
    [pscustomobject]@{Name='UnrelatedPolicy'}
}
function Remove-NetQosPolicy {
    [CmdletBinding(SupportsShouldProcess)] param($Name, $PolicyStore)
    if ($PolicyStore -ne 'ActiveStore' -or $Name -ne 'ScreenCrosshairWeak_test') { throw 'Wrong removal target' }
    $script:removals++
    if ($script:mode -eq 'remove-fails') { Write-Error 'Removal denied'; return }
    if ($script:mode -ne 'remove-ignored') { $script:exists = $false }
}
function Set-NetIPInterface {
    [CmdletBinding()] param($InterfaceIndex, $AddressFamily, $NlMtuBytes, $PolicyStore)
    if ($PolicyStore -ne 'ActiveStore' -or $AddressFamily -ne 'IPv4') { throw 'Wrong MTU target' }
    if ($script:mode -eq 'mtu-fails' -and $InterfaceIndex -eq 7) { Write-Error 'MTU denied'; return }
    if ($script:mode -ne 'mtu-ignored') { $script:mtus[[int]$InterfaceIndex] = [int]$NlMtuBytes }
}
function Get-NetIPInterface {
    [CmdletBinding()] param($InterfaceIndex, $AddressFamily, $PolicyStore)
    [pscustomobject]@{NlMtu=$script:mtus[[int]$InterfaceIndex]}
}

foreach ($case in @('success','absent','query-fails','remove-fails','remove-ignored','mtu-fails','mtu-ignored','invalid-record')) {
    $script:mode = $case
    $script:exists = $case -ne 'absent'
    $script:removals = 0
    $script:mtus = @{7=576; 8=800}
    $records = if ($case -eq 'invalid-record') {'bad'} else {'7|1500;8|1400'}
    $code = $builder.Invoke($null, @('ScreenCrosshairWeak_test', $records))
    $output = @(& ([scriptblock]::Create($code)))
    $expected = $case -eq 'success' -or $case -eq 'absent'
    if (($output -contains 'RESTORE|OK') -ne $expected) { throw "Incorrect result for ${case}: $output" }
    if ($case -eq 'absent' -and $script:removals -ne 0) { throw 'Absent policy should be idempotent' }
    if ($case -eq 'mtu-fails' -and $script:mtus[8] -ne 1400) { throw 'Failure must not skip other adapters' }
    if ($case -eq 'success' -and ($script:exists -or $script:mtus[7] -ne 1500 -or $script:mtus[8] -ne 1400)) { throw 'Incomplete recovery' }
    Write-Host "PASS $case"
}

# A newer recovery record includes the MTU applied by the app. If another tool
# changed it afterwards, recovery must leave that external change untouched.
$script:mode = 'conflict'
$script:exists = $true
$script:removals = 0
$script:mtus = @{7=1100}
$code = $builder.Invoke($null, @('ScreenCrosshairWeak_test', '7|1500|576'))
$output = @(& ([scriptblock]::Create($code)))
if ($output -contains 'RESTORE|OK') { throw 'External MTU change must not be overwritten' }
if (-not ($output -like 'MTUSKIP|7|1100|576')) { throw "Missing MTUSKIP diagnostic: $output" }
if ($script:mtus[7] -ne 1100) { throw 'External MTU value was changed' }
Write-Host 'PASS mtu-conflict-is-preserved'

# A retry must accept adapters restored during a previous partial success.
$script:mode = 'mtu-fails'
$script:exists = $false
$script:mtus = @{7=576;8=576}
$code = $builder.Invoke($null, @('', '7|1500|576;8|1400|576'))
$first = @(& ([scriptblock]::Create($code)))
if ($first -contains 'RESTORE|OK' -or $script:mtus[8] -ne 1400) { throw 'Expected partial restore' }
$script:mode = 'success'
$second = @(& ([scriptblock]::Create($code)))
if ($second -notcontains 'RESTORE|OK' -or $script:mtus[7] -ne 1500) { throw "Retry failed: $second" }
$third = @(& ([scriptblock]::Create($code)))
if ($third -notcontains 'RESTORE|OK') { throw 'Repeated restore must be idempotent' }
Write-Host 'PASS partial restore retry and repeated restore'
foreach ($invalid in @('7|1500|bad', '7|1500|0', '7|1500|576|extra')) {
    $script:mtus = @{7=1100}
    $code = $builder.Invoke($null, @('', $invalid))
    $output = @(& ([scriptblock]::Create($code)))
    if ($output -contains 'RESTORE|OK' -or $script:mtus[7] -ne 1100) { throw 'Malformed records must not modify an adapter' }
}
Write-Host 'PASS malformed records cannot bypass conflict protection'

$mtuBuilder = $type.GetMethod('BuildMtuScript', [Reflection.BindingFlags]'Static,NonPublic')
$journal = Join-Path ([IO.Path]::GetTempPath()) ("crosshair-recovery's-" + [Guid]::NewGuid().ToString('N') + '.tmp')
function Get-NetIPInterface {
    [CmdletBinding()] param($InterfaceIndex, $AddressFamily, $PolicyStore)
    if ($InterfaceIndex) {
        if ($script:verifyFailure -and $InterfaceIndex -eq 7) { throw 'Verification query failed' }
        [pscustomobject]@{InterfaceIndex=$InterfaceIndex; NlMtu=$script:mtus[[int]$InterfaceIndex]; ConnectionState='Connected'}
    } else {
        foreach ($index in @(7,8)) {
            [pscustomobject]@{InterfaceIndex=$index; NlMtu=$script:mtus[$index]; ConnectionState='Connected'}
        }
    }
}
function Set-NetIPInterface {
    [CmdletBinding()] param($InterfaceIndex, $AddressFamily, $NlMtuBytes, $PolicyStore)
    $script:setCalls++
    $saved = [IO.File]::ReadAllText($journal)
    if ($saved -notlike '*7|1500|576*' -or $saved -notlike '*8|1400|576*') { throw 'Missing durable recovery data before mutation' }
    $script:mtus[[int]$InterfaceIndex] = [int]$NlMtuBytes
}
try {
    $script:mtus = @{7=1500;8=1400}
    $script:setCalls = 0
    $script:verifyFailure = $true
    $code = $mtuBuilder.Invoke($null, @(576, [string]$journal))
    $output = @(& ([scriptblock]::Create($code)))
    if ($script:setCalls -ne 2 -or $script:mtus[7] -ne 576 -or -not ($output -like 'MTUERR|7|*')) { throw "Mutation/verification failure fixture failed: $output" }
    if ([IO.File]::ReadAllText($journal) -notlike '*7|1500|576*') { throw 'Verification failure lost original MTU' }
    Write-Host 'PASS journal precedes mutations and survives verification failure'

    $script:setCalls = 0
    $script:mtus = @{7=1500;8=1400}
    $code = $mtuBuilder.Invoke($null, @(576, [string](Join-Path $journal 'missing/records')))
    $output = @(& ([scriptblock]::Create($code)))
    if ($script:setCalls -ne 0 -or -not ($output -like 'MTUERR|*')) { throw 'Unwritable journal must prevent MTU changes' }
    Write-Host 'PASS unwritable journal prevents network changes'
} finally {
    if (Test-Path -LiteralPath $journal) { Remove-Item -LiteralPath $journal -Force }
}
