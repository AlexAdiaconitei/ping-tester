<#
.SYNOPSIS
    Test de conexion continuo mediante ping, con guardado de resultados y visor HTML.

.DESCRIPTION
    Hace ping periodico a uno o varios objetivos (por defecto 1.1.1.1 y 8.8.8.8) durante
    un tiempo determinado, mostrando en consola cada intento (OK/FALLO). Guarda los
    resultados en un CSV (linea a linea, a prueba de cortes) y en un JSON al finalizar.
    Al terminar (por tiempo o al pulsar Ctrl+C), genera automaticamente una copia de
    visor_ping.html con los resultados ya cargados y la abre en el navegador.

.PARAMETER DurationMinutes
    Duracion maxima del test, en minutos. Por defecto 30.

.PARAMETER IntervalSeconds
    Segundos de espera entre cada ping (aproximado, admite decimales). Por defecto 1.

.PARAMETER Targets
    Lista de IPs u hosts a los que hacer ping, alternando entre ellos. Por defecto
    1.1.1.1 y 8.8.8.8.

.PARAMETER Help
    Muestra esta ayuda y sale sin ejecutar el test.

.EXAMPLE
    .\ping_test.ps1
    Test estandar: 30 minutos, cada 1 segundo, contra 1.1.1.1 y 8.8.8.8.

.EXAMPLE
    .\ping_test.ps1 -DurationMinutes 10
    Test de 10 minutos con el resto de opciones por defecto.

.EXAMPLE
    .\ping_test.ps1 -Targets 1.1.1.1
    Test usando un unico objetivo en vez de alternar entre dos.

.EXAMPLE
    .\ping_test.ps1 -DurationMinutes 5 -IntervalSeconds 0.5 -Targets 1.1.1.1,8.8.8.8,192.168.1.1
    Test de 5 minutos, ping cada medio segundo, contra tres objetivos (incluye tu router,
    util para saber si el corte es de tu red local o de internet).

.NOTES
    Para detener el test antes de tiempo: Ctrl+C. Los resultados recogidos hasta ese
    momento se guardan igualmente.
    Tambien puedes ver esta ayuda con: Get-Help .\ping_test.ps1 -Full
#>

param(
    [int]$DurationMinutes = 30,
    [double]$IntervalSeconds = 1,
    [string[]]$Targets = @("1.1.1.1", "8.8.8.8"),
    [switch]$Help
)

if ($Help) {
    Write-Host ""
    Write-Host "PING TEST - ayuda" -ForegroundColor Cyan
    Write-Host "=================="
    Write-Host ""
    Write-Host "Uso:"
    Write-Host "  .\ping_test.ps1 [-DurationMinutes <n>] [-IntervalSeconds <n>] [-Targets <ip1,ip2,...>]"
    Write-Host ""
    Write-Host "Opciones:" -ForegroundColor Yellow
    Write-Host "  -DurationMinutes <n>   Duracion del test en minutos. Por defecto: 30"
    Write-Host "  -IntervalSeconds <n>   Segundos entre cada ping (admite decimales, ej 0.5). Por defecto: 1"
    Write-Host "  -Targets <ip1,ip2,..>  Objetivos a los que hacer ping, separados por coma. Por defecto: 1.1.1.1,8.8.8.8"
    Write-Host "  -Help                  Muestra esta ayuda y sale"
    Write-Host ""
    Write-Host "Ejemplos:" -ForegroundColor Yellow
    Write-Host "  .\ping_test.ps1"
    Write-Host "      Test estandar de 30 minutos contra 1.1.1.1 y 8.8.8.8"
    Write-Host ""
    Write-Host "  .\ping_test.ps1 -DurationMinutes 10"
    Write-Host "      Test de 10 minutos"
    Write-Host ""
    Write-Host "  .\ping_test.ps1 -Targets 1.1.1.1"
    Write-Host "      Solo contra un objetivo"
    Write-Host ""
    Write-Host "  .\ping_test.ps1 -DurationMinutes 5 -IntervalSeconds 0.5 -Targets 1.1.1.1,8.8.8.8,192.168.1.1"
    Write-Host "      Test de 5 min, cada 0.5s, incluyendo tu router (192.168.1.1) para saber"
    Write-Host "      si un corte es de tu red local o de internet"
    Write-Host ""
    Write-Host "Durante la ejecucion:"
    Write-Host "  Ctrl+C   Detiene el test y guarda lo recogido hasta ese momento"
    Write-Host ""
    Write-Host "Al terminar:"
    Write-Host "  Se generan ping_test_<fecha>.csv, ping_test_<fecha>.json y"
    Write-Host "  ping_test_<fecha>_resultado.html (se abre solo en el navegador si"
    Write-Host "  visor_ping.html esta en la misma carpeta que este script)."
    Write-Host ""
    Write-Host "Ayuda detallada (parametros con descripcion): Get-Help .\ping_test.ps1 -Full" -ForegroundColor DarkGray
    Write-Host ""
    exit 0
}

$results   = New-Object System.Collections.Generic.List[Object]
$startTime = Get-Date
$endTime   = $startTime.AddMinutes($DurationMinutes)
$stamp     = Get-Date -Format "yyyyMMdd_HHmmss"
$csvPath   = Join-Path (Get-Location) "ping_test_$stamp.csv"
$jsonPath  = Join-Path (Get-Location) "ping_test_$stamp.json"

"Timestamp,Target,Success,LatencyMs,ErrorMessage" | Out-File -FilePath $csvPath -Encoding UTF8

function Save-Results {
    Write-Host ""
    Write-Host "Guardando resultados... ($($results.Count) muestras)" -ForegroundColor Yellow
    $resultsJson = $results | ConvertTo-Json -Depth 3
    $resultsJson | Out-File -FilePath $jsonPath -Encoding UTF8
    Write-Host "CSV  -> $csvPath"
    Write-Host "JSON -> $jsonPath"

    # Generar copia del visor con los resultados ya embebidos y abrirla automaticamente
    $templatePath = Join-Path $PSScriptRoot "visor_ping.html"
    if (Test-Path $templatePath) {
        $htmlOutPath = Join-Path (Get-Location) "ping_test_${stamp}_resultado.html"
        $template = Get-Content -Path $templatePath -Raw -Encoding UTF8
        $template = $template.Replace("let EMBEDDED_DATA = null;", "let EMBEDDED_DATA = $resultsJson;")
        $template | Out-File -FilePath $htmlOutPath -Encoding UTF8
        Write-Host "HTML -> $htmlOutPath" -ForegroundColor Cyan
        Start-Process $htmlOutPath
    }
    else {
        Write-Host "No se encontro visor_ping.html junto al script ($PSScriptRoot); abrelo manualmente y arrastra el CSV o JSON." -ForegroundColor DarkYellow
    }
}

Write-Host "Test de conexion iniciado durante $DurationMinutes minutos" -ForegroundColor Cyan
Write-Host "Objetivos: $($Targets -join ', ')"
Write-Host "Pulsa Ctrl+C en cualquier momento para detener y guardar." -ForegroundColor Cyan
Write-Host ""

try {
    $i = 0
    while ((Get-Date) -lt $endTime) {
        $target = $Targets[$i % $Targets.Count]
        $i++
        $now = Get-Date

        $success = $false
        $latency = $null
        $errMsg  = ""

        $ping = Test-Connection -ComputerName $target -Count 1 -ErrorAction SilentlyContinue

        if ($ping -and $ping.StatusCode -eq 0) {
            $success = $true
            $latency = $ping.ResponseTime
        }
        else {
            $success = $false
            $errMsg  = if ($ping) { "StatusCode $($ping.StatusCode)" } else { "Sin respuesta / timeout" }
        }

        $entry = [PSCustomObject]@{
            Timestamp    = $now.ToString("yyyy-MM-dd HH:mm:ss.fff")
            Target       = $target
            Success      = $success
            LatencyMs    = $latency
            ErrorMessage = $errMsg
        }
        $results.Add($entry) | Out-Null

        "$($entry.Timestamp),$($entry.Target),$($entry.Success),$($entry.LatencyMs),$($entry.ErrorMessage)" |
            Add-Content -Path $csvPath -Encoding UTF8

        if ($success) {
            Write-Host "$($entry.Timestamp) | $target | OK    | ${latency} ms" -ForegroundColor Green
        }
        else {
            Write-Host "$($entry.Timestamp) | $target | FALLO | $errMsg" -ForegroundColor Red
        }

        Start-Sleep -Seconds $IntervalSeconds
    }
}
finally {
    Save-Results
}
