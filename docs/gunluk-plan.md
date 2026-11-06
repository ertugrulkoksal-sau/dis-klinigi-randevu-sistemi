# Günlük Çalışma Planı (6–15 Kasım 2026)

## Çalışma düzeni

- Her gün o günün adımı ayrı bir branch'te yapılıyor ve Pull Request açılıyor.
- PR incelenip onaylandıktan sonra aynı gün `main`'e **merge commit** ile birleştiriliyor
  (squash veya rebase kullanılmıyor; böylece günlük commit'ler geçmişte olduğu gibi kalıyor).
  Her gün `main`'e en az bir anlamlı değişiklik giriyor.
- Arayüz tasarımı onay beklerken arka uç işi (alan modeli, kurallar, veritabanı) devam ediyor;
  ekranlar tasarım onaylandıktan sonra (11 Kasım ve sonrası) koda dökülüyor.
- **14 Kasım akşamı teslim edilebilir .zip hazır oluyor.** 15 Kasım'da sadece belge ve
  küçük düzeltmeler yapılıyor, .zip son haliyle yeniden üretiliyor.
- README'deki ödev maddesi eşleme tablosu her gün o günün işiyle birlikte güncelleniyor.

## Günlük özet şablonu

Her günün sonunda şu başlıklarla kısa bir özet yazılıyor:

1. Bugün ne yapıldı
2. Nasıl test edilir
3. PR bağlantısı ve commit mesajları
4. **Bugün denemen gerekenler:** 2–3 somut adım (komut ya da tarayıcıda yapılacak iş)

## Plan

| Gün | Branch | Yapılacak iş | Nasıl kontrol edilir |
|---|---|---|---|
| 6 Kas Cum | `chore/proje-iskeleti` | Alan kararları, tasarım dokümanı, bu plan. Çözüm iskeleti: Core / Web / Tests projeleri, EF Core + SQLite, ilk migration, açılışta örnek veri, DI ile ilk repository. Teslim betiği. | `dotnet run` → ana sayfada üç şube listeleniyor; `dotnet test` geçiyor. |
| 7 Kas Cmt | `feature/alan-modeli` | Arayüz tasarımı kanvasta (tüm ekranlar, onaya sunulur). Core: `TimeRange` değer nesnesi, şube çalışma saatleri, bölüm, hizmet, kullanıcı hiyerarşisi, hekim ve müsaitlik aralıkları. Birim testleri. | Kanvas bağlantısı; `dotnet test` (aralık kesişimi, sınır durumları, müsaitliğin çalışma saatine sığması). |
| 8 Kas Paz | `feature/randevu-durumlari` | `Appointment` ve State deseni; tüm geçerli/geçersiz geçişler, 24 saat iptal kuralı, rol bazlı onay/iptal yetkisi (çok biçimli `CanApprove`, `CanCancel`). | Geçiş tablosundaki her hücre için test. |
| 9 Kas Pzt | `feature/cakisma-kontrolu` | `IBookingRule` kuralları, `AppointmentScheduler`, otomatik onay, boş saat üretimi. Eş zamanlılık kararı (K19). | Her kural için ayrı testler + ünit kapasitesi tarama testi. |
| 10 Kas Sal | `feature/veritabani` | EF Core eşlemeleri (TPH, çoka-çok, değer nesneleri), migration, repository'ler, zengin örnek veri (şubeler, hekimler, müsaitlikler, geçmiş/gelecek randevular). | Veritabanı silinip `dotnet run` ile sıfırdan oluşuyor; SQLite üzerinde repository testleri. |
| 11 Kas Çar | `feature/kimlik-dogrulama` | Çerez kimlik doğrulaması, `PasswordHasher`, rol politikaları, giriş/kayıt ekranları (onaylı tasarımla), ortak sayfa düzeni. README'ye örnek hesaplar. | Örnek hesaplarla giriş; yetkisiz sayfaya erişim engelleniyor; veritabanında parolalar hash'li. |
| 12 Kas Per | `feature/hasta-randevu` | Hasta randevu alma akışı, randevularım, iptal. | Boş saat seçip randevu alma; dolu saatin listede görünmemesi; 24 saat kuralı. |
| 13 Kas Cum | `feature/personel-paneli` | Hekim/asistan paneli: günlük takvim, onay kuyruğu (onayla/reddet), tamamlandı/gelmedi, müsaitlik yönetimi. README'ye UML sınıf ve sıra diyagramları (koddan). | Bekleyen randevuyu onaylama; onaylı randevuda "onayla" butonunun olmaması. |
| 14 Kas Cmt | `feature/yonetici-paneli` | Yönetici paneli: şube, çalışma saatleri, bölüm, hizmet, personel; şube yöneticisinin sadece kendi şubesini görmesi. README'nin son hali: SOLID tablosu, tasarım kararları özeti, eşleme tablosunun tamamı. Merge sonrası **teslim .zip'i**. | Şube yöneticisi hesabıyla başka şubeye erişememe; .zip açılıp `dotnet run` ile çalışıyor. |
| 15 Kas Paz | `docs/son-duzeltmeler` | Sadece belge ve küçük düzeltmeler; temiz klonla deneme; .zip son haliyle yeniden üretilir. SABİS'e yükleme (23:59'dan önce). | Temiz klasörde `dotnet run` ve `dotnet test`. |

**Teslimden sonra (Aralık başına kadar):** 10–12 slaytlık sunum (konuşma notlarıyla),
5 dakikalık canlı demo senaryosu ve olası sorulara kısa cevaplar. Sunum Mac'te açılabilir formatta.

## Riskler

- **Tasarım onayı gecikirse:** 7–10 Kasım işleri arayüzden bağımsız; ekran işleri en erken 11 Kasım'da başlıyor.
- **14 Kasım yoğun:** Yönetici paneli ve README son hali aynı gün. UML diyagramları bu yüzden 13 Kasım'a alındı; eşleme tablosu zaten her gün güncelleniyor.
- **macOS farkı:** Windows'a özgü hiçbir şey kullanılmıyor (SQLite dosya veritabanı, http, `TimeProvider`); 14–15 Kasım'da .zip'ten temiz kurulum deneniyor. Teslimden önce bir kez Mac'te çalıştırmak gerekiyor.
