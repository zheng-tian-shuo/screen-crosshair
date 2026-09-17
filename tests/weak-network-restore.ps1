# Run after build/build.ps1. Mock cmdlets ensure this test never changes networking.
$ErrorActionPreference = 'Stop'
$exe = Get-Item (Join-Path (Split-Path $PSScriptRoot -Parent) 'build\* v6.0.exe')
$assembly = [Reflection.Assembly]::LoadFile($exe.FullName)
$type = $assembly.GetType('ScreenCrosshair.WeakNetworkOps')
$builder = $type.GetMethod('BuildRestoreScript', [Reflection.BindingFlags]'Static,NonPublic')

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
