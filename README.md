# Diş Kliniği Randevu Sistemi

BSM515 Nesneye Dayalı Programlama dersi bireysel projesi (Seçenek A: Hastane/Klinik
Randevu Sistemi). Ödevi çok şubeli bir diş kliniği zincirine uyarladım: hastalar şube,
bölüm, hizmet ve hekim seçerek randevu alıyor; sistem hekim, hasta ve şubedeki diş
koltuğu (ünit) çakışmalarını kontrol ediyor; tedavi randevuları hekim ya da asistan
onayından geçiyor.

> Proje geliştirme aşamasında. Günlük ilerleme için [docs/gunluk-plan.md](docs/gunluk-plan.md),
> alan modeli ve tasarım kararları için [docs/tasarim.md](docs/tasarim.md).

## Teknolojiler

- ASP.NET Core MVC, .NET 10 (LTS)
- Entity Framework Core 10 + SQLite (dosya veritabanı, ayrı sunucu gerekmiyor)
- xUnit (birim testleri)

Windows'a özgü hiçbir bileşen kullanılmıyor; proje macOS, Linux ve Windows'ta aynı şekilde çalışıyor.

## Kurulum ve çalıştırma

Gereken tek şey [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

### macOS

```bash
# .NET 10 SDK yoksa (Homebrew ile):
brew install --cask dotnet-sdk

git clone https://github.com/ertugrulkoksal-sau/dis-klinigi-randevu-sistemi.git
cd dis-klinigi-randevu-sistemi
dotnet run --project src/DisKlinigi.Web
```

Tarayıcıda `http://localhost:5080` adresini açın.

### Windows / Linux

Aynı komutlar geçerli: `dotnet run --project src/DisKlinigi.Web`.

### İlk çalıştırmada ne oluyor?

`src/DisKlinigi.Web/dis-klinigi.db` adında bir SQLite dosyası oluşturuluyor, şema
migration'larla kuruluyor ve örnek veri (şubeler vb.) otomatik yükleniyor. Sıfırdan başlamak
için uygulamayı kapatıp bu dosyayı silmek yeterli.

### Testler

```bash
dotnet test
```

## Proje yapısı

```
src/
  DisKlinigi.Core/     Alan sınıfları, iş kuralları, arayüzler (altyapıdan bağımsız)
  DisKlinigi.Web/      MVC arayüzü, EF Core veri erişimi, örnek veri
tests/
  DisKlinigi.Tests/    Core için birim testleri
docs/
  tasarim.md           Alan modeli, iş kuralları, tasarım kararları ve alternatifleri
  gunluk-plan.md       6–15 Kasım çalışma planı
```

## Ödev maddelerinin karşılandığı yerler

Tablo proje ilerledikçe dolduruluyor; "Durum" sütunu o maddenin hangi gün tamamlandığını gösteriyor.

| Ödev maddesi | Sistemde karşılığı | Durum |
|---|---|---|
| Tek klinik ve çok şubeli kuruluş desteği | Zincir → şube modeli; tek klinik = tek şubeli zincir (tasarım K7) | İskelet: `Branch` (6 Kas) |
| Klinikler yetki çerçevesinde tanımlanır | Zincir yöneticisi tüm şubeleri, şube yöneticisi sadece kendi şubesini yönetir (K3) | Planlandı (7, 14 Kas) |
| Çalışma saatleri ve bölümler | Şubenin haftalık çalışma saatleri, şubenin barındırdığı bölümler | Planlandı (7 Kas) |
| Hizmetler süre ve ücretiyle | `Service`: süre, ücret, onay gereksinimi | Planlandı (7 Kas) |
| Doktor ve sağlık personeli kaydı | `Dentist`, `Assistant` (kullanıcı hiyerarşisi) | Planlandı (7 Kas) |
| Doktorun uzmanlık alanı ve verdiği hizmetler | Hekimin uzmanlık bölümü ve hizmet listesi | Planlandı (7 Kas) |
| Müsaitlik aralıkları; randevu sadece bu aralıklara | Şube bazlı haftalık müsaitlik + `DentistAvailabilityRule` | Planlandı (7, 9 Kas) |
| Personel ve hizmet seçerek randevu | Şube → bölüm → hizmet → hekim → saat akışı | Planlandı (12 Kas) |
| Çakışma kontrolü | `IBookingRule` kuralları + `AppointmentScheduler` | Planlandı (9 Kas) |
| Randevu detaylarının kaydı | Randevuda hizmet, süre, ücret, hekim, şube, tarih/saat (K9) | Planlandı (8 Kas) |
| Onay mekanizması | Hizmete göre otomatik/manuel onay, State deseni (K2, K13) | Planlandı (8–9, 13 Kas) |
| Roller: Hasta, Doktor/Personel, Yönetici | Hasta, Hekim, Asistan, Şube Yöneticisi, Zincir Yöneticisi (K14) | Planlandı (7, 11 Kas) |

README'nin son halinde (15 Kasım) UML sınıf ve sıra diyagramları, SOLID eşleme tablosu
ve tasarım kararlarının özeti de yer alacak.
