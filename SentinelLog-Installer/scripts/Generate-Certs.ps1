<#
.SYNOPSIS
    Generates TLS certificates for syslog-ng using OpenSSL or PowerShell.
#>
param(
    [string]$OutputDir = ".\certs",
    [string]$CACommonName = "SyslogCA",
    [string]$ServerCommonName = "syslog-server",
    [int]$ValidDays = 3650
)

$ErrorActionPreference = "Continue"

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$openssl = Get-Command openssl -ErrorAction SilentlyContinue

if ($openssl) {
    Write-Host "  Using OpenSSL: $($openssl.Source)" -ForegroundColor Gray

    & openssl genrsa -out "$OutputDir\ca.key" 4096 2>&1 | Out-Null
    & openssl req -new -x509 -days $ValidDays -key "$OutputDir\ca.key" -out "$OutputDir\ca.crt" -subj "/CN=$CACommonName" 2>&1 | Out-Null
    & openssl genrsa -out "$OutputDir\server.key" 4096 2>&1 | Out-Null
    & openssl req -new -key "$OutputDir\server.key" -out "$OutputDir\server.csr" -subj "/CN=$ServerCommonName" 2>&1 | Out-Null
    & openssl x509 -req -days $ValidDays -in "$OutputDir\server.csr" -CA "$OutputDir\ca.crt" -CAkey "$OutputDir\ca.key" -CAcreateserial -out "$OutputDir\server.crt" 2>&1 | Out-Null
    Remove-Item "$OutputDir\server.csr" -ErrorAction SilentlyContinue

    Write-Host "  TLS certificates generated using OpenSSL." -ForegroundColor Green
} else {
    Write-Host "  OpenSSL not found. Using PowerShell." -ForegroundColor Yellow

    $caCert = New-SelfSignedCertificate `
        -Subject "CN=$CACommonName" `
        -CertStoreLocation "Cert:\LocalMachine\My" `
        -KeyExportPolicy Exportable `
        -KeySpec Signature `
        -KeyLength 4096 `
        -KeyAlgorithm RSA `
        -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddDays($ValidDays) `
        -TextExtension @("2.5.29.19={critical}{text}ca=TRUE")

    $serverCert = New-SelfSignedCertificate `
        -Subject "CN=$ServerCommonName" `
        -CertStoreLocation "Cert:\LocalMachine\My" `
        -KeyExportPolicy Exportable `
        -KeySpec KeyExchange `
        -KeyLength 4096 `
        -KeyAlgorithm RSA `
        -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddDays($ValidDays) `
        -Signer $caCert

    # Export CA cert PEM
    $caCertBytes = $caCert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
    $caCertPem = "-----BEGIN CERTIFICATE-----`n" + [Convert]::ToBase64String($caCertBytes, [Base64FormattingOptions]::InsertLineBreaks) + "`n-----END CERTIFICATE-----"
    $caCertPem | Out-File -FilePath "$OutputDir\ca.crt" -Encoding ASCII

    # Export server cert PEM
    $serverCertBytes = $serverCert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
    $serverCertPem = "-----BEGIN CERTIFICATE-----`n" + [Convert]::ToBase64String($serverCertBytes, [Base64FormattingOptions]::InsertLineBreaks) + "`n-----END CERTIFICATE-----"
    $serverCertPem | Out-File -FilePath "$OutputDir\server.crt" -Encoding ASCII

    # Export server private key PEM
    try {
        $pfxPass = ConvertTo-SecureString -String "tempexport" -Force -AsPlainText
        $pfxPath = "$OutputDir\server.pfx"
        Export-PfxCertificate -Cert $serverCert -FilePath $pfxPath -Password $pfxPass | Out-Null

        $pfxColl = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2Collection
        $pfxColl.Import($pfxPath, "tempexport", [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
        foreach ($cert in $pfxColl) {
            if ($cert.HasPrivateKey) {
                $rsaKey = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
                $keyBytes = $rsaKey.ExportRSAPrivateKey()
                $keyPem = "-----BEGIN RSA PRIVATE KEY-----`n" + [Convert]::ToBase64String($keyBytes, [Base64FormattingOptions]::InsertLineBreaks) + "`n-----END RSA PRIVATE KEY-----"
                $keyPem | Out-File -FilePath "$OutputDir\server.key" -Encoding ASCII
                break
            }
        }
        Remove-Item $pfxPath -ErrorAction SilentlyContinue
    } catch {
        Write-Host "  Warning: Could not export private key as PEM." -ForegroundColor Yellow
    }

    # Cleanup cert store
    Remove-Item "Cert:\LocalMachine\My\$($caCert.Thumbprint)" -ErrorAction SilentlyContinue
    Remove-Item "Cert:\LocalMachine\My\$($serverCert.Thumbprint)" -ErrorAction SilentlyContinue

    Write-Host "  TLS certificates generated using PowerShell." -ForegroundColor Green
}

# Create CA hash link if OpenSSL available
if ($openssl -and (Test-Path "$OutputDir\ca.crt")) {
    $caHash = & openssl x509 -hash -noout -in "$OutputDir\ca.crt" 2>$null
    if ($caHash) {
        Copy-Item "$OutputDir\ca.crt" "$OutputDir\${caHash}.0" -Force
    }
}

Write-Host "  Certificates saved to: $OutputDir" -ForegroundColor Green
