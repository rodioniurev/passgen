<#
  Проверки переводов интерфейса. Окон не открывают: собранный PassGen.exe загружается в память,
  таблица Loc.Table и исходники src\*.cs проверяются на полноту и согласованность.

  Запуск (из корня репозитория, после build.bat):
    powershell -NoProfile -ExecutionPolicy Bypass -File tests\LocalizationTests.ps1

  Код возврата: 0 — всё в порядке, 1 — есть ошибки, 2 — не найден PassGen.exe.
#>
param([string]$Exe = (Join-Path $PSScriptRoot '..\PassGen.exe'), [string]$Src = (Join-Path $PSScriptRoot '..\src'))

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Exe)) { Write-Host "Не найден $Exe — сначала запустите build.bat"; exit 2 }

$asm = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Resolve-Path $Exe)))
$loc = $asm.GetType('PassGen.Loc')
$flags = [Reflection.BindingFlags]'Public,NonPublic,Static'
$table = $loc.GetField('Table', $flags).GetValue($null)
$codes = $loc.GetField('Codes', $flags).GetValue($null)
$names = $loc.GetField('Names', $flags).GetValue($null)
$init  = $loc.GetMethod('Init', $flags)
$detect = $loc.GetMethod('Detect', $flags)
$t1    = $loc.GetMethod('T', $flags, $null, [Type[]]@([string]), $null)
$plural = $loc.GetMethod('Plural', $flags)
$code  = $loc.GetProperty('Code', $flags)

function T([string]$key) { [string]$t1.Invoke($null, [object[]]@($key)) }
function Plural([int]$n, [string]$key) { [string]$plural.Invoke($null, [object[]]@($n, $key)) }

$script:fail = 0
function Check([string]$name, [bool]$ok, [string]$info = '') {
    if ($ok) { "  ok   $name" } else { $script:fail++; "  FAIL $name  $info" }
}

$cyr = '[Ѐ-ӿ]'
$colEn = $codes.IndexOf('en') + 1
$colRu = $codes.IndexOf('ru') + 1

"--- 1. таблица переводов ($($table.Count) строк, языки: $($codes -join ', '))"
Check 'у каждого языка есть название' ($names.Count -eq $codes.Count)
$badCols = @($table | ? { $_.Count -ne $codes.Count + 1 } | % { $_[0] })
Check 'в каждой строке столько колонок, сколько языков' ($badCols.Count -eq 0) ($badCols -join ', ')
$dup = @($table | Group-Object { $_[0] } | ? { $_.Count -gt 1 } | % { $_.Name })
Check 'ключи не повторяются' ($dup.Count -eq 0) ($dup -join ', ')
$empty = @(); foreach ($row in $table) { for ($c = 1; $c -le $codes.Count; $c++) { if ([string]::IsNullOrWhiteSpace($row[$c])) { $empty += "$($row[0])[$($codes[$c-1])]" } } }
Check 'нет пустых переводов' ($empty.Count -eq 0) ($empty -join ', ')

$badPh = @()
foreach ($row in $table) {
    $sets = for ($c = 1; $c -le $codes.Count; $c++) { (([regex]::Matches($row[$c], '\{\d+\}') | % { $_.Value } | Sort-Object -Unique) -join ',') }
    if (@($sets | Sort-Object -Unique).Count -ne 1) { $badPh += $row[0] }
}
Check 'подстановки {0}, {1} совпадают во всех языках' ($badPh.Count -eq 0) ($badPh -join ', ')

$badFmt = @()
foreach ($row in $table) { for ($c = 1; $c -le $codes.Count; $c++) { try { [void][string]::Format($row[$c], 'a', 'b', 'c') } catch { $badFmt += "$($row[0])[$($codes[$c-1])]" } } }
Check 'все строки годятся для string.Format (нет лишних фигурных скобок)' ($badFmt.Count -eq 0) ($badFmt -join ', ')

$cyrWrong = @(); foreach ($row in $table) { for ($c = 1; $c -le $codes.Count; $c++) { if ($c -ne $colRu -and $row[$c] -match $cyr) { $cyrWrong += "$($row[0])[$($codes[$c-1])]" } } }
Check 'в английских и испанских строках нет кириллицы' ($cyrWrong.Count -eq 0) ($cyrWrong -join ', ')
$latinRu = @($table | ? { $_[$colRu] -notmatch $cyr -and $_[$colRu] -cne $_[$colEn] } | % { $_[0] })
Check 'русский перевод не остался английским (кроме одинаковых «Enter», «F1» и т. п.)' ($latinRu.Count -eq 0) ($latinRu -join ', ')

"--- 2. ключи в исходниках и в таблице"
$tableKeys = @($table | % { $_[0] })
$used = @{}
foreach ($f in Get-ChildItem $Src -Filter *.cs | ? { $_.Name -ne 'Loc.cs' }) {
    foreach ($m in [regex]::Matches([IO.File]::ReadAllText($f.FullName), '"((?:popup|tip|help|menu|tray|toast|prob|msg|dlg|unit)\.[A-Za-z0-9.]+)"')) { $used[$m.Groups[1].Value] = $f.Name }
}
$missing = @($used.Keys | ? { $tableKeys -notcontains $_ } | % { "$_ ($($used[$_]))" })
Check 'каждый ключ из исходников есть в таблице (нет опечаток)' ($missing.Count -eq 0) ($missing -join ', ')
$unused = @($tableKeys | ? { -not $used.ContainsKey($_) })
Check 'каждый ключ таблицы где-то используется' ($unused.Count -eq 0) ($unused -join ', ')

"--- 3. подсказка значка в трее — не длиннее 63 знаков (иначе программа падает при запуске)"
foreach ($c in $codes) {
    [void]$init.Invoke($null, [object[]]@($c))
    $over = @()
    foreach ($hk in $false, $true) { foreach ($quick in $false, $true) {
        $t = (T 'tray.head') + $(if ($hk) { T 'tray.hotkey' } else { '' }) + (T 'tray.window') + $(if ($quick) { T 'tray.quick' } else { '' })
        if ($t.Length -gt 63) { $over += "$($t.Length): $t" }
    } }
    Check "[$c] все четыре варианта подсказки укладываются" ($over.Count -eq 0) ($over -join ' | ')
}

"--- 4. выбор языка"
$expectExit = @{ en = 'Exit'; es = 'Salir'; ru = 'Выход' }
foreach ($c in $codes) { [void]$init.Invoke($null, [object[]]@($c)); Check "язык $c выбирается явно" ($code.GetValue($null) -ceq $c -and (T 'menu.exit') -ceq $expectExit[$c]) }
[void]$init.Invoke($null, [object[]]@('auto'))
$det = [string]$detect.Invoke($null, $null)
Check "«auto» берёт язык Windows ($det)" ($code.GetValue($null) -ceq $det -and $codes -contains $det)
[void]$init.Invoke($null, [object[]]@('xx'))
Check 'незнакомый язык не ломает программу — берётся язык Windows' ($code.GetValue($null) -ceq $det)
Check 'нет такого ключа → возвращается сам ключ' ((T 'no.such.key') -ceq 'no.such.key')

"--- 5. множественное число"
[void]$init.Invoke($null, [object[]]@('ru'))
$ru = @{ 1 = 'знак'; 2 = 'знака'; 4 = 'знака'; 5 = 'знаков'; 11 = 'знаков'; 12 = 'знаков'; 14 = 'знаков'; 21 = 'знак'; 22 = 'знака'; 25 = 'знаков'; 100 = 'знаков'; 101 = 'знак'; 111 = 'знаков' }
$bad = @($ru.Keys | ? { (Plural $_ 'unit.char') -cne $ru[$_] } | % { "$_ -> $(Plural $_ 'unit.char')" })
Check 'русский: 1 знак, 2 знака, 5 знаков, 11 знаков, 21 знак…' ($bad.Count -eq 0) ($bad -join ', ')
[void]$init.Invoke($null, [object[]]@('en'))
Check 'английский: 1 character, 2 characters, 0 characters' ((Plural 1 'unit.char') -ceq 'character' -and (Plural 2 'unit.char') -ceq 'characters' -and (Plural 0 'unit.char') -ceq 'characters')
[void]$init.Invoke($null, [object[]]@('es'))
Check 'испанский: 1 carácter, 2 caracteres' ((Plural 1 'unit.char') -ceq 'carácter' -and (Plural 2 'unit.char') -ceq 'caracteres')

''
if ($script:fail -eq 0) { 'Итог: ВСЁ ОК'; exit 0 } else { "Итог: ПРОВАЛОВ: $($script:fail)"; exit 1 }
