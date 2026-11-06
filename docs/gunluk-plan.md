# Günlük Çalışma Planı (6–15 Kasım 2026)

Her gün ayrı bir branch'te çalışıp Pull Request açıyorum; inceleme sonrası aynı gün
`main`'e birleştiriliyor. Böylece her gün `main`'e en az bir anlamlı değişiklik giriyor.
Arayüz tasarımı onay beklerken arka uç işi (alan modeli, kurallar, veritabanı) devam ediyor;
ekranlar ancak tasarım onaylandıktan sonra (11 Kasım ve sonrası) koda dökülüyor.

| Gün | Branch | Yapılacak iş | Nasıl kontrol edilir |
|---|---|---|---|
| 6 Kas Cum | `chore/proje-iskeleti` | Alan kararları, tasarım dokümanı, bu plan. Çözüm iskeleti: Core / Web / Tests projeleri, EF Core + SQLite, ilk migration, açılışta örnek veri, DI ile ilk repository. | `dotnet run` → ana sayfada üç şube listeleniyor; `dotnet test` geçiyor. |
| 7 Kas Cmt | `feature/alan-modeli` | Arayüz tasarımı kanvasta (tüm ekranlar, onaya sunulur). Core: `TimeRange` değer nesnesi, şube çalışma saatleri, bölüm, hizmet, kullanıcı hiyerarşisi, hekim ve müsaitlik aralıkları. Birim testleri. | Kanvas bağlantısı; `dotnet test` (aralık kesişimi, sınır durumları, müsaitliğin çalışma saatine sığması). |
| 8 Kas Paz | `feature/randevu-durumlari` | `Appointment` ve State deseni; tüm geçerli/geçersiz geçişler, 24 saat iptal kuralı, rol bazlı onay/iptal yetkisi (çok biçimli `CanApprove`, `CanCancel`). | Geçiş tablosundaki her hücre için test. |
| 9 Kas Pzt | `feature/cakisma-kontrolu` | `IBookingRule` kuralları, `AppointmentScheduler`, otomatik onay, boş saat üretimi. Eş zamanlılık kararı (K19). | Her kural için ayrı testler + ünit kapasitesi tarama testi. |
| 10 Kas Sal | `feature/veritabani` | EF Core eşlemeleri (TPH, çoka-çok, değer nesneleri), migration, repository'ler, zengin örnek veri (şubeler, hekimler, müsaitlikler, geçmiş/gelecek randevular). | Veritabanı silinip `dotnet run` ile sıfırdan oluşuyor; SQLite üzerinde repository testleri. |
| 11 Kas Çar | `feature/kimlik-dogrulama` | Çerez kimlik doğrulaması, `PasswordHasher`, rol politikaları, giriş/kayıt ekranları (onaylı tasarımla), ortak sayfa düzeni. | Örnek hesaplarla giriş; yetkisiz sayfaya erişim engelleniyor; veritabanında parolalar hash'li. |
| 12 Kas Per | `feature/hasta-randevu` | Hasta randevu alma akışı, randevularım, iptal. | Boş saat seçip randevu alma; dolu saatin listede görünmemesi; 24 saat kuralı. |
| 13 Kas Cum | `feature/personel-paneli` | Hekim/asistan paneli: günlük takvim, onay kuyruğu (onayla/reddet), tamamlandı/gelmedi, müsaitlik yönetimi. | Bekleyen randevuyu onaylama; onaylı randevuda "onayla" butonunun olmaması. |
| 14 Kas Cmt | `feature/yonetici-paneli` | Yönetici paneli: şube, çalışma saatleri, bölüm, hizmet, personel; şube yöneticisinin sadece kendi şubesini görmesi. | Şube yöneticisi hesabıyla başka şubeye erişememe. |
| 15 Kas Paz | `docs/readme-ve-teslim` | README son hali (UML sınıf ve sıra diyagramı, SOLID tablosu, ödev maddesi eşleme tablosu, kurulum), son düzeltmeler, temiz klonla deneme, SABİS için .zip. | Temiz klasörde `dotnet run` ve `dotnet test`. |

**Teslimden sonra (Aralık başına kadar):** 10–12 slaytlık sunum (konuşma notlarıyla),
5 dakikalık canlı demo senaryosu ve olası sorulara kısa cevaplar.

## Riskler

- **Tasarım onayı gecikirse:** 7–10 Kasım işleri arayüzden bağımsız; ekran işleri en erken 11 Kasım'da başlıyor.
- **macOS farkı:** Windows'a özgü hiçbir şey kullanılmıyor (SQLite dosya veritabanı, http, `TimeProvider`); 15 Kasım'da temiz klonla tekrar deneniyor. Mümkünse teslimden önce bir kez Mac'te çalıştırmak gerekiyor.
