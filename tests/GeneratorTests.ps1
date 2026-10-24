<#
  Автотесты генератора паролей. Окон не открывают и клавиш не нажимают: собранный PassGen.exe
  загружается в память, а классы Gen и Typer проверяются через reflection.

  Запуск (из корня репозитория, после build.bat):
    powershell -NoProfile -ExecutionPolicy Bypass -File tests\GeneratorTests.ps1

  Код возврата: 0 — все проверки прошли, 1 — есть провалы, 2 — не найден PassGen.exe.
#>
param([string]$Exe = (Join-Path $PSScriptRoot '..\PassGen.exe'))

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Exe)) { Write-Host "Не найден $Exe — сначала запустите build.bat"; exit 2 }

# Загружаем из байтов: файл не блокируется, и PassGen.exe можно пересобрать, пока идут тесты.
$asm   = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Resolve-Path $Exe)))
$gen   = $asm.GetType('PassGen.Gen')
$sett  = $asm.GetType('PassGen.Settings')
$typer = $asm.GetType('PassGen.Typer')
$make  = $gen.GetMethod('Make')
$clean = $typer.GetMethod('Clean')

function New-S([hashtable]$o) {
    $s = [Activator]::CreateInstance($sett)
    foreach ($k in $o.Keys) { $s.$k = $o[$k] }
    $s
}

function Run($s, [int]$n) {
    $res = New-Object System.Collections.Generic.List[string]
    $bits = 0.0
    for ($i = 0; $i -lt $n; $i++) {
        $a = [object[]]@($s, [double]0)
        $res.Add([string]$make.Invoke($null, $a))
        $bits = $a[1]
    }
    [pscustomobject]@{ List = $res; Bits = $bits }
}

function Clean([string]$text) { [string]$clean.Invoke($null, [object[]]@($text)) }

$script:fail = 0
function Check([string]$name, [bool]$ok, [string]$info = '') {
    if ($ok) { "  ok   $name" } else { $script:fail++; "  FAIL $name  $info" }
}

$symRx = '[!@#$%^&*\-_=+?]'

'--- 1. по умолчанию (16 знаков, все наборы)'
$r = Run (New-S @{}) 20000
$l = $r.List
Check 'длина = 16' (-not ($l | ? { $_.Length -ne 16 }))
Check 'есть строчная'  (-not ($l | ? { $_ -cnotmatch '[a-z]' }))
Check 'есть заглавная' (-not ($l | ? { $_ -cnotmatch '[A-Z]' }))
Check 'есть цифра'     (-not ($l | ? { $_ -notmatch '[0-9]' }))
Check 'есть спецзнак'  (-not ($l | ? { $_ -notmatch $symRx }))
Check 'нет посторонних знаков' (-not ($l | ? { $_ -cnotmatch '^[A-Za-z0-9!@#$%^&*\-_=+?]+$' }))
Check 'все пароли разные (20000 шт.)' (($l | Sort-Object -Unique).Count -eq 20000)
$expectedBits = 16 * [math]::Log(75, 2)
Check ("оценка стойкости {0:N1} бит (ожидаем {1:N1})" -f $r.Bits, $expectedBits) ([math]::Abs($r.Bits - $expectedBits) -lt 0.1)
$cnt = @{}
foreach ($p in $l) { foreach ($c in $p.ToCharArray()) { if ($c -cmatch '[a-z]') { $cnt[$c] = 1 + [int]$cnt[$c] } } }
$vals = $cnt.Values
$mean = ($vals | Measure-Object -Average).Average
# критерий хи-квадрат: 26 букв, 25 степеней свободы; порог 60 даёт ложное срабатывание примерно раз в 10 000 запусков
$chi = ($vals | % { ($_ - $mean) * ($_ - $mean) / $mean } | Measure-Object -Sum).Sum
Check ("строчные буквы распределены равномерно (хи-квадрат {0:N1}, порог 60)" -f $chi) ($cnt.Count -eq 26 -and $chi -lt 60)

'--- 2. «Удобно вводить»'
$r = Run (New-S @{ Easy = $true }) 20000; $l = $r.List
Check 'нет похожих знаков 0 O 1 l I |' (-not ($l | ? { $_ -cmatch '[0O1lI|]' }))
Check 'нет «трудных» спецзнаков' (-not ($l | ? { $_ -cmatch '[\\/`''"~^<>()\[\]{};:.,]' }))
Check 'длина = 16' (-not ($l | ? { $_.Length -ne 16 }))
Check 'все наборы на месте' (-not ($l | ? { $_ -cnotmatch '[a-z]' -or $_ -cnotmatch '[A-Z]' -or $_ -notmatch '[2-9]' -or $_ -notmatch '[!@#$%&*\-_=+?]' }))
$r2 = Run (New-S @{ Easy = $true; SymbolSet = '!@#$%^&*()-_=+[]{};:,.<>/?' }) 5000
Check 'длинный набор сужается до простого' (-not ($r2.List | ? { $_ -cmatch '[\\/`''"~^<>()\[\]{};:.,]' }))
$r3 = Run (New-S @{ Easy = $true; SymbolSet = '[]{}' }) 2000
Check 'только «трудные» знаки → запасной простой набор' (-not ($r3.List | ? { $_ -cnotmatch '[!@#$%&*\-_=+?]' }))

'--- 3. блоки через «-»'
$bad = @()
foreach ($n in 4..128) {
    $s = New-S @{ Length = $n; Blocks = $true }
    foreach ($p in (Run $s 30).List) {
        if ($p.Length -ne $n) { $bad += "длина $n -> $($p.Length) [$p]"; break }
        if ($p -match '--' -or $p.StartsWith('-') -or $p.EndsWith('-')) { $bad += "дефисы $n [$p]"; break }
        foreach ($blk in $p.Split('-')) { if ($n -ge 8 -and ($blk.Length -lt 3 -or $blk.Length -gt 7)) { $bad += "блок $n [$p]"; break } }
    }
}
Check 'длина с дефисами равна заданной (4..128), блоки по 3–7 знаков' ($bad.Count -eq 0) ($bad[0..2] -join ' | ')
$rb = Run (New-S @{ Length = 24; Blocks = $true; SymbolSet = '-' }) 3000
Check 'дефис не попадает внутрь блоков (набор «-» заменяется запасным)' (-not ($rb.List | ? { ($_ -replace '-', '') -cnotmatch '[!@#$%^&*_=+?]' }))

'--- 4. начинать с буквы'
$l = (Run (New-S @{ StartLetter = $true }) 20000).List
Check 'первый знак — буква' (-not ($l | ? { $_ -cnotmatch '^[A-Za-z]' }))
$l = (Run (New-S @{ StartLetter = $true; Upper = $false }) 5000).List
Check 'только строчные → первая строчная' (-not ($l | ? { $_ -cnotmatch '^[a-z]' }))
$l = (Run (New-S @{ StartLetter = $true; Blocks = $true; Length = 20 }) 5000).List
Check 'вместе с блоками' (-not ($l | ? { $_ -cnotmatch '^[A-Za-z]' }))
$l = (Run (New-S @{ StartLetter = $true; Upper = $false; Lower = $false; Symbols = $false }) 1000).List
Check 'букв в наборе нет → просто цифры, без ошибки' (-not ($l | ? { $_ -notmatch '^[0-9]{16}$' }))

'--- 5. минимум спецзнаков'
$l = (Run (New-S @{ MinSymbols = 3; Length = 12 }) 20000).List
Check 'не меньше 3 спецзнаков' (-not ($l | ? { ([regex]::Matches($_, $symRx)).Count -lt 3 }))
$l = (Run (New-S @{ MinSymbols = 8; Length = 4 }) 2000).List
Check 'длина 4 при минимуме 8 → без ошибок, длина 4' (-not ($l | ? { $_.Length -ne 4 }))

'--- 6. нестандартные наборы и границы'
$l = (Run (New-S @{ Upper = $false; Lower = $false; Symbols = $false; Length = 6 }) 3000).List
Check 'PIN: только цифры' (-not ($l | ? { $_ -notmatch '^[0-9]{6}$' }))
$l = (Run (New-S @{ Upper = $false; Lower = $false; Digits = $false; Symbols = $false }) 100).List
Check 'ни одного набора → строчные' (-not ($l | ? { $_ -cnotmatch '^[a-z]{16}$' }))
$l = (Run (New-S @{ SymbolSet = 'абв123  ' }) 1000).List
Check 'буквы, цифры и пробелы в наборе спецзнаков игнорируются' (-not ($l | ? { $_ -cnotmatch '^[A-Za-z0-9!@#$%^&*\-_=+?]+$' }))
$l = (Run (New-S @{ SymbolSet = '€№§'; Length = 30 }) 1000).List
Check 'свои юникод-спецзнаки допускаются' (-not ($l | ? { $_ -notmatch '[€№§]' }))
$l = (Run (New-S @{ Length = 128 }) 500).List
Check 'длина 128' (-not ($l | ? { $_.Length -ne 128 }))
$l = (Run (New-S @{ Length = 9999 }) 10).List
Check 'слишком большая длина обрезается до 128' (-not ($l | ? { $_.Length -ne 128 }))

'--- 7. «Удобно вводить»: спецзнаков ровно столько, сколько задано'
$sym = '[!@#$%&*\-_=+?]'
$l = (Run (New-S @{ Easy = $true }) 20000).List
Check 'ровно 1 спецзнак при минимуме 1' (-not ($l | ? { ([regex]::Matches($_, $sym)).Count -ne 1 }))
$l = (Run (New-S @{ Easy = $true; MinSymbols = 3; Length = 20 }) 20000).List
Check 'ровно 3 спецзнака при минимуме 3' (-not ($l | ? { ([regex]::Matches($_, $sym)).Count -ne 3 }))
$l = (Run (New-S @{ Easy = $true; Blocks = $true; Length = 20 }) 20000).List
Check 'с блоками: дефисы-разделители и ровно 1 спецзнак внутри' (-not ($l | ? { ([regex]::Matches(($_ -replace '-', ''), $sym)).Count -ne 1 -or $_.Length -ne 20 }))
$l = (Run (New-S @{ Easy = $true; Upper = $false; Lower = $false; Digits = $false }) 2000).List
Check 'только спецзнаки → пароль из спецзнаков, без ошибок' (-not ($l | ? { $_ -cnotmatch '^[!@#$%&*\-_=+?]{16}$' }))
$l = (Run (New-S @{ Easy = $true; Symbols = $false }) 3000).List
Check 'без спецзнаков → только буквы и цифры без похожих' (-not ($l | ? { $_ -cnotmatch '^[A-HJ-NP-Za-km-z2-9]{16}$' }))
$a = Run (New-S @{ Easy = $true }) 1
$expected = 15 * [math]::Log(57, 2) + [math]::Log(12, 2) + [math]::Log(16, 2)
Check ("оценка стойкости в удобном режиме {0:N1} бит (ожидаем {1:N1})" -f $a.Bits, $expected) ([math]::Abs($a.Bits - $expected) -lt 0.5)

'--- 8. подготовка текста из буфера к посимвольному вводу (Right Alt+V)'
Check 'пробелы и перевод строки по краям убираются' ((Clean "  user@example.com `r`n") -ceq 'user@example.com')
Check 'перевод строки внутри → один пробел' ((Clean "a`r`nb") -ceq 'a b')
Check 'пустые строки и табуляция → один пробел' ((Clean "a`r`n`r`n`tb") -ceq 'a b')
Check 'управляющие знаки выбрасываются' ((Clean ("a" + [char]0 + "b" + [char]7)) -ceq 'ab')
Check 'одни пробелы → пустая строка' ((Clean '   ') -ceq '')
Check 'кириллица и символы не портятся' ((Clean 'привет, мир! №1') -ceq 'привет, мир! №1')

''
if ($script:fail -eq 0) { 'Итог: ВСЁ ОК'; exit 0 } else { "Итог: ПРОВАЛОВ: $($script:fail)"; exit 1 }
