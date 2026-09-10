param(
    [string]$Profile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'local/smoke'),
    [ValidateSet('click','drag','scroll','key','text')][string]$Kind = 'click',
    [int]$X,
    [int]$Y,
    [int]$EndX,
    [int]$EndY,
    [ValidateSet('left','right','middle')][string]$Button = 'left',
    [int]$WheelDelta = -120,
    [string]$Key,
    [ValidateLength(1,2048)][string]$Text,
    [string]$TextBase64
)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
if (!$IsWindows -and $PSVersionTable.PSEdition -ne 'Desktop') { throw 'Windows is required.' }
if ($Kind -in @('click','drag','scroll') -and (!$PSBoundParameters.ContainsKey('X') -or !$PSBoundParameters.ContainsKey('Y'))) { throw 'Mouse input requires X and Y.' }
if ($Kind -eq 'drag' -and (!$PSBoundParameters.ContainsKey('EndX') -or !$PSBoundParameters.ContainsKey('EndY'))) { throw 'Drag requires EndX and EndY.' }
if ($TextBase64) { $Text = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($TextBase64)) }
if ($Kind -eq 'text' -and (!$Text -or $Text.Length -gt 2048)) { throw 'Text must contain 1..2048 UTF-16 characters.' }
if ($Kind -eq 'key' -and !$Key) { throw 'Key is required, for example Escape or Ctrl+A.' }
if (!(('RimCarbon14.DesktopMouse' -as [type]))) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace RimCarbon14 {
    public static class DesktopMouse {
        [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Explicit)] struct InputData { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Keyboard; }
        [StructLayout(LayoutKind.Sequential)] struct Input { public uint Type; public InputData Data; }
        [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr window, ref Point point);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] static extern bool AttachThreadInput(uint from, uint to, bool attach);
        [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr window);
        public static void Focus(IntPtr window) {
            ShowWindow(window, 9);
            if (SetForegroundWindow(window)) return;
            uint pid;
            var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out pid);
            var currentThread = GetCurrentThreadId();
            bool attached = foregroundThread != 0 && foregroundThread != currentThread && AttachThreadInput(currentThread, foregroundThread, true);
            try { BringWindowToTop(window); SetForegroundWindow(window); }
            finally { if (attached) AttachThreadInput(currentThread, foregroundThread, false); }
        }
        [DllImport("user32.dll", SetLastError=true)] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll", SetLastError=true)] static extern uint SendInput(uint count, Input[] input, int size);
        public static void Send(uint flags, int data) {
            Send(new Input { Data = new InputData { Mouse = new MouseInput { Flags = flags, Data = unchecked((uint)data) } } });
        }
        public static void Keyboard(ushort key, ushort scan, uint flags) {
            if ((key >= 0x21 && key <= 0x28) || key == 0x2E) flags |= 1;
            Send(new Input { Type = 1, Data = new InputData { Keyboard = new KeyboardInput { Key = key, Scan = scan, Flags = flags } } });
        }
        static void Send(Input input) {
            var inputs = new[] { input };
            if (SendInput(1, inputs, Marshal.SizeOf(typeof(Input))) != 1)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput failed; check desktop and process integrity levels.");
        }
    }
}
'@
}
$discovery = Get-Content (Join-Path $Profile 'RimCarbon14/discovery.json') -Raw | ConvertFrom-Json
$headers = @{ Authorization = 'Bearer ' + $discovery.token }
$status = Invoke-RestMethod ($discovery.endpoint + 'v1/status') -Headers $headers
if ($status.instanceId -ne $discovery.instanceId -or $status.pid -ne $discovery.pid) { throw 'Bridge instance changed. Read the new layout before sending input.' }
$game = Get-Process -Id $discovery.pid
if ($game.ProcessName -ne 'RimWorldWin64' -or !$game.MainWindowHandle) { throw 'The bridge process has no RimWorld window.' }
$window = $game.MainWindowHandle
$dpi = [RimCarbon14.DesktopMouse]::SetThreadDpiAwarenessContext([IntPtr](-4))
$pressed = $false
$up = 0
$keysDown = New-Object 'System.Collections.Generic.List[System.UInt16]'
$mutex = New-Object Threading.Mutex($false, 'Local\RimCarbon14.DesktopInput')
$leased = $false
try {
    try { $leased = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $leased = $true }
    if (!$leased) { throw 'Another RimCarbon14 desktop input operation is running.' }
    [RimCarbon14.DesktopMouse]::Focus($window)
    Start-Sleep -Milliseconds 200
    function Assert-GameForeground {
        if ([RimCarbon14.DesktopMouse]::GetForegroundWindow() -ne $window) { throw 'Game is not foreground. Focus its window and retry; no further input was sent.' }
        [uint32]$owner = 0
        [RimCarbon14.DesktopMouse]::GetWindowThreadProcessId($window, [ref]$owner) | Out-Null
        if ($owner -ne $discovery.pid) { throw 'Game window ownership changed.' }
    }
    Assert-GameForeground
    $rect = New-Object RimCarbon14.DesktopMouse+Rect
    $origin = New-Object RimCarbon14.DesktopMouse+Point
    if (![RimCarbon14.DesktopMouse]::GetClientRect($window, [ref]$rect) -or ![RimCarbon14.DesktopMouse]::ClientToScreen($window, [ref]$origin)) { throw 'Cannot resolve game client bounds.' }
    $width = $rect.Right - $rect.Left; $height = $rect.Bottom - $rect.Top
    function Move-ToClient([int]$ClientX, [int]$ClientY) {
        Assert-GameForeground
        if ($ClientX -lt 0 -or $ClientY -lt 0 -or $ClientX -ge $width -or $ClientY -ge $height) { throw "Point is outside the game client (${width}x${height})." }
        if (![RimCarbon14.DesktopMouse]::SetCursorPos($origin.X + $ClientX, $origin.Y + $ClientY)) { throw 'SetCursorPos failed.' }
    }
    if ($Kind -eq 'drag' -and ($EndX -lt 0 -or $EndY -lt 0 -or $EndX -ge $width -or $EndY -ge $height)) { throw 'Drag endpoint is outside the game client.' }
    if ($Kind -eq 'key') {
        $codes = @{ Ctrl=0x11; Alt=0x12; Shift=0x10; Escape=0x1B; Enter=0x0D; Tab=0x09; Backspace=0x08; Delete=0x2E; Space=0x20; Home=0x24; End=0x23; Left=0x25; Up=0x26; Right=0x27; Down=0x28; PageUp=0x21; PageDown=0x22 }
        $parts = $Key.Split('+')
        $keyCodes = foreach ($part in $parts) {
            if ($codes.ContainsKey($part)) { [uint16]$codes[$part] }
            elseif ($part -match '^[a-zA-Z0-9]$') { [uint16][char]$part.ToUpperInvariant() }
            elseif ($part -match '^F([1-9]|1[0-2])$') { [uint16](0x6F + [int]$Matches[1]) }
            else { throw "Unsupported key: $part" }
        }
        foreach ($code in $keyCodes) {
            Assert-GameForeground
            [RimCarbon14.DesktopMouse]::Keyboard($code, 0, 0); $keysDown.Add($code)
        }
        Start-Sleep -Milliseconds 80
    }
    elseif ($Kind -eq 'text') {
        foreach ($character in $Text.ToCharArray()) {
            Assert-GameForeground
            try { [RimCarbon14.DesktopMouse]::Keyboard(0, [uint16]$character, 4) }
            finally { [RimCarbon14.DesktopMouse]::Keyboard(0, [uint16]$character, 6) }
        }
    }
    else {
        Move-ToClient $X $Y
        Start-Sleep -Milliseconds 80
        Assert-GameForeground
        if ($Kind -eq 'scroll') { [RimCarbon14.DesktopMouse]::Send(0x800, $WheelDelta) }
        else {
        $down, $up = switch ($Button) { left { 0x2; 0x4 }; right { 0x8; 0x10 }; middle { 0x20; 0x40 } }
        [RimCarbon14.DesktopMouse]::Send($down, 0); $pressed = $true
        if ($Kind -eq 'drag') {
            for ($step = 1; $step -le 12; $step++) {
                Start-Sleep -Milliseconds 20
                Move-ToClient ([int]($X + ($EndX - $X) * $step / 12)) ([int]($Y + ($EndY - $Y) * $step / 12))
            }
        } else { Start-Sleep -Milliseconds 80 }
        [RimCarbon14.DesktopMouse]::Send($up, 0); $pressed = $false
        }
    }
    [pscustomobject]@{ sent=$true; verifiedEffect=$false; kind=$Kind; pid=$game.Id; instanceId=$discovery.instanceId; clientWidth=$width; clientHeight=$height } | ConvertTo-Json -Compress
} finally {
    try {
        if ($pressed) { [RimCarbon14.DesktopMouse]::Send($up, 0) }
        for ($i=$keysDown.Count-1; $i -ge 0; $i--) { [RimCarbon14.DesktopMouse]::Keyboard($keysDown[$i], 0, 2) }
    } finally {
        if ($dpi -ne [IntPtr]::Zero) { [RimCarbon14.DesktopMouse]::SetThreadDpiAwarenessContext($dpi) | Out-Null }
        if ($leased) { $mutex.ReleaseMutex() }; $mutex.Dispose(); $game.Dispose()
    }
}
