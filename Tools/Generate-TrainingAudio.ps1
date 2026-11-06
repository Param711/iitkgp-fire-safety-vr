param([switch]$TheoryOnly, [switch]$ControlsOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$audioDirectory = Join-Path $projectRoot 'Assets/FireSafety/Audio'
New-Item -ItemType Directory -Force -Path $audioDirectory | Out-Null
$speech = New-Object -ComObject SAPI.SpVoice
$voices = $speech.GetVoices()
$speech.Rate = 0
$speech.Volume = 100
$lines = @(
    @('BriefOrdinary', 'This assessment involves ordinary combustible materials, such as paper and wood. Complete Theory and Quiz, and review Controls before you begin. Your decisions will be reviewed after the session.', 0),
    @('BriefElectrical', 'This assessment involves energized electrical equipment. Complete Theory and Quiz, and review Controls before you begin. Your decisions will be reviewed after the session.', 0),
    @('BriefLiquid', 'This assessment involves a flammable liquid spill. Complete Theory and Quiz, and review Controls before you begin. Your decisions will be reviewed after the session.', 0),
    @('ControlsDesktop', 'Desktop controls. W, A, S, D to walk. Hold the right mouse button to look. Aim and press E to interact. G picks up or releases equipment. P removes the extinguisher pin. Hold the left mouse button to spray or pour. Tab opens controls. Escape releases the mouse.', 0),
    @('ControlsVR', 'Quest controller controls. Left stick to walk. Right stick for a thirty degree snap turn. Aim the right controller ray. Green means an interactive target. Press the right trigger for menus, doors, alarms and taps. Tap the right grip once to pick up equipment, and tap it again to return the equipment. You can reach nearby equipment or point at it. The extinguisher cylinder is held in the left hand and the nozzle in the right. Press the left trigger to remove the pin. Hold the right trigger to spray or pour, and release it to stop. Release the trigger after picking up equipment or closing a panel. Press the left Y button for session controls. If tracking is lost, reconnect both controllers, release all buttons and centre the sticks before continuing.', 0),
    @('Equipment', 'Equipment orientation, before assessment. The red wall panel marked Fire Alarm has a white button. Aim at it and interact to sound the building alarm. The Emergency Call panel or office telephone simulates a request for assistance. For the extinguisher: pull the pin, aim at the base, squeeze, and sweep from side to side. Use portable equipment only for a small fire when it is safe and you have a clear escape route. Do not use water on energized electrical equipment or flammable liquid. To fill a bucket, open a tap, hold the bucket below the outlet, then close the tap. Aim away from objects and hold the pour control.', 0),
    @('SoundCheck', 'Sound check. You should hear this voice clearly. During an assessment, you will also hear nearby occupants, fire sounds, and the alarm after it is activated.', 0),
    @('AlarmAcknowledged', 'Building fire alarm activated.', 0),
    @('CallAcknowledged', 'This is a simulated assistance request. Your floor has been recorded. Evacuation or rescue has not been completed.', 0),
    @('NPCFireA', 'Fire! There is a fire here!', 0),
    @('NPCFireB', 'There is smoke! Fire!', 1),
    @('NPCAlarmA', 'I can hear the alarm.', 0),
    @('NPCAlarmB', 'The fire alarm is sounding.', 1),
    @('NPCWaitingA', 'I am here. I need assistance.', 0),
    @('NPCWaitingB', 'Can anyone hear me? I am waiting here.', 1)
)
if ($TheoryOnly) { $lines = @() }
if ($ControlsOnly) { $lines = @($lines | Where-Object { $_[0] -eq 'ControlsVR' }) }
$course = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Assets/FireSafety/Resources/FireSafetyTheory.json') | ConvertFrom-Json
for ($lessonIndex = 0; !$ControlsOnly -and $lessonIndex -lt $course.pages.Count; $lessonIndex++) {
    $lines += ,@(('Theory{0:00}' -f ($lessonIndex + 1)), $course.pages[$lessonIndex].narration, 0)
}
for ($questionIndex = 0; !$ControlsOnly -and $questionIndex -lt $course.questions.Count; $questionIndex++) {
    $lines += ,@(('TheoryQuestion{0:00}' -f ($questionIndex + 1)), $course.questions[$questionIndex].narration, 0)
    $lines += ,@(('TheoryAnswer{0:00}' -f ($questionIndex + 1)), ($course.questions[$questionIndex].explanation -replace '\r?\n', ' '), 0)
}
try {
    foreach ($line in $lines) {
        $voiceIndex = [Math]::Min([int]$line[2], $voices.Count - 1)
        $speech.Voice = $voices.Item($voiceIndex)
        $stream = New-Object -ComObject SAPI.SpFileStream
        try {
            $stream.Format.Type = 22 # 22.05 kHz, 16-bit, mono PCM.
            $stream.Open((Join-Path $audioDirectory ($line[0] + '.wav')), 3, $false)
            $speech.AudioOutputStream = $stream
            [void]$speech.Speak($line[1], 0)
            $stream.Close()
        } finally {
            [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($stream)
        }
        Write-Output ('Created ' + $line[0] + '.wav')
    }
} finally {
    [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($speech)
}

if ($TheoryOnly -or $ControlsOnly) { return }
# Deterministic loop for the extinguisher nozzle; contains no recorded speech.
$sampleRate = 22050
$noise = New-Object System.Random 1729
$sampleCount = $sampleRate * 2
$file = [System.IO.File]::Create((Join-Path $audioDirectory 'ExtinguisherSpray.wav'))
$writer = New-Object System.IO.BinaryWriter $file
try {
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes('RIFF'))
    $writer.Write([int](36 + $sampleCount * 2))
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes('WAVEfmt '))
    $writer.Write([int]16); $writer.Write([int16]1); $writer.Write([int16]1)
    $writer.Write([int]$sampleRate); $writer.Write([int]($sampleRate * 2))
    $writer.Write([int16]2); $writer.Write([int16]16)
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes('data'))
    $writer.Write([int]($sampleCount * 2))
    $previous = 0.0
    for ($sampleIndex = 0; $sampleIndex -lt $sampleCount; $sampleIndex++) {
        $current = $noise.NextDouble() * 2 - 1
        $filtered = ($current - $previous) * 0.22
        $previous = $current
        $writer.Write([int16]($filtered * 32767))
    }
} finally { $writer.Dispose() }
Write-Output 'Created ExtinguisherSpray.wav'
