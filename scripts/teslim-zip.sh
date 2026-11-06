#!/usr/bin/env bash
# SABİS'e yüklenecek .zip dosyasını üretir.
#
# Sadece git'e eklenmiş dosyalar pakete giriyor (git archive); bin/obj klasörleri
# ve yerel SQLite veritabanı otomatik olarak dışarıda kalıyor. Paketlemeden önce
# testler çalıştırılıyor, test geçmezse .zip üretilmiyor.
#
# Kullanım (çözüm klasöründe):  ./scripts/teslim-zip.sh
set -euo pipefail

cd "$(dirname "$0")/.."

if [ -n "$(git status --porcelain)" ]; then
    echo "Uyarı: kaydedilmemiş değişiklikler var; .zip sadece son commit'i içerir." >&2
fi

echo "Testler çalıştırılıyor..."
dotnet test --nologo --verbosity quiet

mkdir -p teslim
cikti="teslim/dis-klinigi-randevu-sistemi.zip"
git archive --format=zip --prefix=dis-klinigi-randevu-sistemi/ -o "$cikti" HEAD

echo "Hazır: $cikti ($(git rev-parse --short HEAD) commit'inden)"
