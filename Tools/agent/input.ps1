# Синтетический ввод в окно Unity Game View через Win32 SendInput.
# Нужен потому, что игра читает старый Input Manager: он опрашивает состояние
# клавиатуры в ОС, и подделать его изнутри редактора нельзя.
#
#   input.ps1 -Script "key:W:1200,mouse:300:0,tap:Space"
#
# Команды: key:<КЛАВИША>:<мс>  tap:<КЛАВИША>  mouse:<dx>:<dy>  lmb  rmb  wait:<мс>
param(
    [Parameter(Mandatory = $true)][string]$Script,
    [int]$UnityPid = 0
)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Sim {
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
  [DllImport("user32.dll")] public static extern short VkKeyScan(char ch);
}
"@

$KEYUP = 0x0002
$MOUSE_MOVE = 0x0001
$LDOWN = 0x0002; $LUP = 0x0004
$RDOWN = 0x0008; $RUP = 0x0010

$named = @{
    'space' = 0x20; 'shift' = 0xA0; 'ctrl' = 0x11; 'alt' = 0x12; 'tab' = 0x09
    'esc' = 0x1B; 'enter' = 0x0D; 'g' = 0x47; 'b' = 0x42
    '1' = 0x31; '2' = 0x32; '3' = 0x33; '4' = 0x34
    'z' = 0x5A; 'x' = 0x58; 'c' = 0x43
}

function Get-Vk([string]$name) {
    $key = $name.ToLower()
    if ($named.ContainsKey($key)) { return [byte]$named[$key] }
    if ($name.Length -eq 1) { return [byte]([Sim]::VkKeyScan($name[0]) -band 0xFF) }
    throw "неизвестная клавиша: $name"
}

if ($UnityPid -gt 0) {
    $shell = New-Object -ComObject WScript.Shell
    [void]$shell.AppActivate($UnityPid)
    Start-Sleep -Milliseconds 400
}

foreach ($step in $Script.Split(',')) {
    $parts = $step.Trim().Split(':')
    switch ($parts[0].ToLower()) {
        'key' {
            $vk = Get-Vk $parts[1]
            $ms = if ($parts.Count -gt 2) { [int]$parts[2] } else { 200 }
            [Sim]::keybd_event($vk, 0, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds $ms
            [Sim]::keybd_event($vk, 0, $KEYUP, [UIntPtr]::Zero)
        }
        'tap' {
            $vk = Get-Vk $parts[1]
            [Sim]::keybd_event($vk, 0, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 60
            [Sim]::keybd_event($vk, 0, $KEYUP, [UIntPtr]::Zero)
        }
        'mouse' {
            # Дробим на мелкие шаги: один большой скачок игра прочитает как выброс дельты.
            $dx = [int]$parts[1]; $dy = [int]$parts[2]
            $steps = 12
            for ($i = 0; $i -lt $steps; $i++) {
                [Sim]::mouse_event($MOUSE_MOVE, [int]($dx / $steps), [int]($dy / $steps), 0, [UIntPtr]::Zero)
                Start-Sleep -Milliseconds 16
            }
        }
        'lmb' {
            [Sim]::mouse_event($LDOWN, 0, 0, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 60
            [Sim]::mouse_event($LUP, 0, 0, 0, [UIntPtr]::Zero)
        }
        'rmb' {
            [Sim]::mouse_event($RDOWN, 0, 0, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 60
            [Sim]::mouse_event($RUP, 0, 0, 0, [UIntPtr]::Zero)
        }
        'wait' { Start-Sleep -Milliseconds ([int]$parts[1]) }
        default { throw "неизвестная команда: $step" }
    }
    Start-Sleep -Milliseconds 40
}

Write-Output "done"
