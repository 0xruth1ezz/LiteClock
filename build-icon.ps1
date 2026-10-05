# Convert the generated PNG into a standard multi-resolution Windows ICO.
# Small entries use 32-bit DIBs for WinForms compatibility; 256 uses PNG.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$clockMaster = [Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'LiteClock.png'))
$clockSizes = @(16,20,24,32,40,48,64,128,256)
$clockFrames = [Collections.Generic.List[byte[]]]::new()
try {
    foreach ($clockSize in $clockSizes) {
        $clockBitmap = [Drawing.Bitmap]::new($clockSize,$clockSize,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $clockGraphics = [Drawing.Graphics]::FromImage($clockBitmap)
        try {
            $clockGraphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $clockGraphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
            $clockGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $clockGraphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $clockGraphics.DrawImage($clockMaster,[Drawing.Rectangle]::new(0,0,$clockSize,$clockSize),0,0,$clockMaster.Width,$clockMaster.Height,[Drawing.GraphicsUnit]::Pixel)
            $clockBuffer = [IO.MemoryStream]::new()
            if ($clockSize -eq 256) {
                $clockBitmap.Save($clockBuffer,[Drawing.Imaging.ImageFormat]::Png)
            } else {
                $clockWriter = [IO.BinaryWriter]::new($clockBuffer,[Text.Encoding]::UTF8,$true)
                $clockMaskStride = [int]([Math]::Ceiling($clockSize / 32.0) * 4)
                $clockWriter.Write([uint32]40)
                $clockWriter.Write([int]$clockSize)
                $clockWriter.Write([int]($clockSize * 2))
                $clockWriter.Write([uint16]1)
                $clockWriter.Write([uint16]32)
                $clockWriter.Write([uint32]0)
                $clockWriter.Write([uint32]($clockSize * $clockSize * 4 + $clockMaskStride * $clockSize))
                $clockWriter.Write([int]0); $clockWriter.Write([int]0)
                $clockWriter.Write([uint32]0); $clockWriter.Write([uint32]0)
                for ($clockY = $clockSize - 1; $clockY -ge 0; $clockY--) {
                    for ($clockX = 0; $clockX -lt $clockSize; $clockX++) {
                        $clockPixel = $clockBitmap.GetPixel($clockX,$clockY)
                        $clockWriter.Write([byte]$clockPixel.B); $clockWriter.Write([byte]$clockPixel.G)
                        $clockWriter.Write([byte]$clockPixel.R); $clockWriter.Write([byte]$clockPixel.A)
                    }
                }
                for ($clockY = $clockSize - 1; $clockY -ge 0; $clockY--) {
                    $clockMask = [byte[]]::new($clockMaskStride)
                    for ($clockX = 0; $clockX -lt $clockSize; $clockX++) {
                        if ($clockBitmap.GetPixel($clockX,$clockY).A -eq 0) {
                            $clockByte = [int][Math]::Floor($clockX / 8)
                            $clockMask[$clockByte] = $clockMask[$clockByte] -bor (128 -shr ($clockX % 8))
                        }
                    }
                    $clockWriter.Write($clockMask)
                }
                $clockWriter.Flush(); $clockWriter.Dispose()
            }
            $clockFrames.Add($clockBuffer.ToArray()); $clockBuffer.Dispose()
        } finally { $clockGraphics.Dispose(); $clockBitmap.Dispose() }
    }
    $clockFile = [IO.File]::Create((Join-Path $PSScriptRoot 'LiteClock.ico'))
    $clockWriter = [IO.BinaryWriter]::new($clockFile)
    try {
        $clockWriter.Write([uint16]0); $clockWriter.Write([uint16]1); $clockWriter.Write([uint16]$clockSizes.Count)
        $clockOffset = 6 + 16 * $clockSizes.Count
        for ($clockIndex = 0; $clockIndex -lt $clockSizes.Count; $clockIndex++) {
            $clockDimension = if ($clockSizes[$clockIndex] -eq 256) { 0 } else { $clockSizes[$clockIndex] }
            $clockWriter.Write([byte]$clockDimension); $clockWriter.Write([byte]$clockDimension)
            $clockWriter.Write([byte]0); $clockWriter.Write([byte]0)
            $clockWriter.Write([uint16]1); $clockWriter.Write([uint16]32)
            $clockWriter.Write([uint32]$clockFrames[$clockIndex].Length); $clockWriter.Write([uint32]$clockOffset)
            $clockOffset += $clockFrames[$clockIndex].Length
        }
        foreach ($clockFrame in $clockFrames) { $clockWriter.Write($clockFrame) }
    } finally { $clockWriter.Dispose(); $clockFile.Dispose() }
    Write-Output ('Created LiteClock.ico: ' + ($clockSizes -join ', ') + ' px')
} finally { $clockMaster.Dispose() }
