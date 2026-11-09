# Arayüz Tasarımı

Ekranları kod yazmadan önce tasarım kanvasında çizdim. Kanvas bağlantısı:

**https://claude.ai/artifact/LqSo2athR6pzshgDTkbAhc**

**Onay durumu: onaylandı** (9 Kasım 2026, revizyon 1). 7 Kasım'da onaya sunuldu, 9 Kasım'da
bağımsız inceleme sonrası düzeltildi ve bu haliyle onaylandı. Ekranlar 11 Kasım'dan itibaren
bu revizyona göre kodlanacak.

Kanvas şimdilik sadece bana açık; başkasına göstermek için sayfadaki Paylaş menüsünden paylaşmak gerekiyor.
Onaydan sonra yapılacak tasarım değişiklikleri de önce kanvasta gösterilip sonra koda uygulanacak.

## Ekranlar

| # | Ekran | Kim görüyor | İçerik |
|---|---|---|---|
| 1 | Giriş ve hasta kaydı | Herkes | Tek giriş formu (rol girişten sonra belirleniyor), hasta kayıt formu (ad soyad tek alan, telefon isteğe bağlı), hatalı giriş örneği |
| 2a | Randevu al: seçimler | Hasta | Adım göstergesi; şube → bölüm → hizmet → hekim. Her seçim sonrakini daraltıyor. "Onay gerekir" rozeti. Hekim bulunamayınca boş durum metni. |
| 2b | Randevu al: tarih/saat, özet ve onay | Hasta | Hekimin o şubede çalıştığı günler, kurallardan geçen boş saatler, özet kartı (ücret, süre, onay bilgisi), hekime not, boş gün ve "kaydederken saat dolmuşsa" örnekleri |
| 3 | Randevularım | Hasta | Yaklaşan randevular (iptal; 24 saatten az kaldıysa pasif ve nedeni yazıyor; iptalden önce onay adımı), geçmiş randevular tablosu (süre dahil), tüm durum rozetleri, boş durum |
| 4a | Günlük takvim | Hekim (asistan: hekim seçicili) | Günün randevuları ve boş aralıklar, "şu an" çizgisi; duruma göre Onayla/Reddet…, Tamamlandı/Gelmedi olarak işaretle, İptal et… |
| 4b | Onay kuyruğu | Asistan (hekim: sadece kendi randevuları) | Onay bekleyen randevular, hekim filtresi, hasta notu ve alınma zamanı, ret gerekçesi formu, saati geçmiş bekleyen randevu (sadece Reddet), boş durum |
| 4c | Müsaitlik aralıkları | Hekim | Aralık tablosu (ileri tarihte randevusu olan aralık silinemiyor), aralık ekleme formu ve çalışma saatine sığmama hatası örneği |
| 5a | Şubeler ve çalışma saatleri | Zincir yöneticisi (şube yöneticisi: sadece kendi şubesi) | Şube listesi, şube bilgileri ve ünit sayısı, haftalık çalışma saatleri, barındırılan bölümler |
| 5b | Bölümler ve hizmetler | Zincir yöneticisi (şube yöneticisi salt okunur) | Bölüm kataloğu (ekle, adını değiştir), hizmet tablosu ("Yeni hizmet", satır başına "Düzenle"), hizmet formu ve ücret değişikliğinin eski randevuları etkilemediği uyarısı |
| 5c | Şube personeli | Şube yöneticisi | Şube personeli tablosu, şubeye hekim atama, yeni asistan hesabı |
| 5d | Hekim kaydı | Zincir yöneticisi | Zincirdeki bütün hekimler, yeni hekim formu (uzmanlık, verdiği hizmetler, çalışacağı şubeler) |

Rol farklarını anlatan notlar ekranların içinden kanvas notlarına taşındı; ekranlarda sadece
kullanıcının göreceği metin kaldı. Kesikli çerçeveli kutular boş durum ve hata örnekleri.

## Ortak kararlar

- **Renkler:** açık gri zemin, beyaz kartlar, ana renk petrol yeşili. Durum rozetleri hem renk hem
  yazıyla ayrışıyor (Onay bekliyor, Onaylandı, Reddedildi, İptal edildi, Tamamlandı, Gelmedi,
  Saati geçti); sadece renge güvenmiyorum.
- **Üst menü role göre değişiyor.** Hasta: Randevu al, Randevularım. Hekim: Takvim, Onay kuyruğu,
  Müsaitlik. Asistan: Takvim, Onay kuyruğu. Yönetici: Şubeler (şube yöneticisinde "Şubem"),
  Bölüm ve hizmetler, Personel.
- **Butonlar yetkiye ve duruma göre çıkıyor.** Örneğin onaylı randevuda "Onayla" yok; "Tamamlandı"
  sadece saati gelmiş onaylı randevuda var. Bu, kodda `CanApprove` / `CanCancel` ve State deseninin
  ekrandaki karşılığı.
- **Geri alınamayan işlemler önce sorar.** İptal, ret ve silme butonlarında "…" var; önce onay ya da
  gerekçe isteniyor.
- **Hastaya sadece boş saatler gösteriliyor.** Kaydederken aynı kurallar tekrar çalıştığı için
  arada saat dolarsa çiğnenen kuralların hepsi birlikte gösteriliyor (tasarim.md bölüm 3).
- **Seçim akışı basit formlarla.** Randevu alma adımları JavaScript gerektirmiyor; her seçimde sayfa
  sorgu parametreleriyle yeniden yükleniyor.
- **Telefon genişliği:** menü ve kartlar alt alta diziliyor, tablolar kendi kutusunda yatay kayıyor.
- Sayfalar Bootstrap'in hazır görünümüne yakın tutuldu, böylece kodlarken ek CSS kütüphanesi gerekmeyecek.

## Tasarımda ortaya çıkan küçük kararlar

Ekranları çizerken alan modelinde olmayan birkaç ayrıntı çıktı. Durumları:

1. Boş saatler **15 dakikada bir** başlıyor. *Kodda var (K32, 9 Kasım).*
2. Hasta randevu alırken **hekime kısa bir not** yazabiliyor; kuyrukta randevunun **ne zaman alındığı**
   görünüyor. *Onaylandı (K35), 10 Kasım'da koda girecek.*
3. Reddederken **gerekçe** yazılıyor ve hastaya gösteriliyor. *Kodda var (`RejectionReason`).*
4. İleri tarihte randevusu olan müsaitlik aralığı **silinemiyor**; şube çalışma saati daraltılırken
   dışarıda kalan müsaitlik varsa kayıt engelleniyor. Etkilenen kayıtların listelenmesi kapsam dışı.
5. Personel hesabı yönetici tarafından **geçici parolayla** açılıyor; ilk girişte değiştirme zorunlu
   değil (K36).

## Bağımsız inceleme (9 Kasım)

Tasarımı çizen değil, ayrı bir inceleyici ekranları ödev maddeleri, tasarim.md ve Core'daki kodla
karşılaştırdı. Yüksek önemde bulgu çıkmadı; ödev maddelerinin hepsi en az kısmen karşılanıyordu.
Orta önemdeki bulgular ve yapılanlar:

| Bulgu | Yapılan |
|---|---|
| Boş saat örneği kurallarla üretilemezdi (11:30 ve 12:00 var, 11:45 yok; takvimdeki randevularla çelişiyordu) | Liste takvimdeki randevulardan yeniden hesaplandı: 10:30, 10:45, 11:00, 11:15 |
| K28'deki "saati geçmiş bekleyen randevu" hiçbir ekranda yoktu | Kuyruğa "Saati geçti" rozetli, sadece "Reddet" butonu olan kart eklendi |
| Hekim menüsündeki kuyruk sayısı (3) hekimin göremeyeceği randevuları sayıyordu | Hekim ekranlarında 1 (kendi bekleyen randevusu) |
| İleri tarihte randevusu olan müsaitlik aralığında "Sil" etkindi | Pasif buton ve gerekçe; diğer satırlarda silmeden önce onay |
| Şube yöneticisi hekimin bütün şubeleri etkileyen bilgilerini düzenleyebiliyordu | Yetki ayrıldı: 5c şube yöneticisi, yeni 5d zincir yöneticisi (K34) |
| Yeni personel formu yoktu | 5c'de yeni asistan, 5d'de yeni hekim formu |
| Yeni hizmet için giriş noktası yoktu | "Yeni hizmet" butonu, satır başına "Düzenle", bölüm için "Adı değiştir" |
| İptal tek tıkla yapılıyor gibiydi | Randevularım'a iptal onay adımı eklendi |
| Boş durumlar yoktu | Hekim listesi, boş gün, boş kuyruk ve randevusu olmayan hasta için örnekler |

Düşük önemli bulgulardan yapılanlar: hata örneği "seçim kaldırılır, saat listeden çıkar" olarak
yeniden yazıldı; ret iki ekranda aynı formu kullanıyor; "hekim onayı / hekim veya asistan onayı"
yerine tek ifade "Onay gerekir"; katalogda bölümlerin tam adı; kuyruk filtresine eksik hekim
eklendi; örnek geçmiş tarihler hekimin müsaitliğine ve 15 dakikalık adıma uyduruldu; "Tamamlandı
olarak işaretle" / "Gelmedi olarak işaretle" buton adları; "şu an" çizgisi doğru yere alındı;
pasif butonlar `aria-disabled` ile klavyeyle odaklanabilir; kayıt formunda ad soyad tek alan ve
telefon isteğe bağlı; geçmiş randevularda süre sütunu; süre alanına üst sınır (240); müsaitlik
ekranındaki tabloyla aynı bilgiyi veren haftalık görünüm kaldırıldı.

## Koda yansıyacak işler

İnceleme koddaki birkaç noktayı da gösterdi. Plandaki ilgili günde yapılacaklar:

| Gün | İş |
|---|---|
| 10 Kas | `Appointment`'a hasta notu (en fazla 500 karakter) ve oluşturulma zamanı (K35). `Dentist` hata mesajlarında "Serdivan Şubesi şubesinde" gibi tekrar eden ek düzeltilecek (şube adı zaten "Şubesi" ile bitiyor). |
| 12 Kas | Kural mesajları ekrandaki metinle aynı olacak: "Dt. Ayşe Kaya bu saat aralığında başka bir randevuda." ve "Serdivan Şubesi'ndeki 4 ünitin hepsi bu saat aralığının bir kısmında dolu." |
| 13 Kas | İleri tarihte aktif randevusu olan müsaitlik aralığı silinemeyecek (servis katmanında; `Dentist.RemoveAvailability` şu an koşulsuz). `BranchManager.CanCancel` ve `ChainAdministrator.CanCancel` `false` olacak, testleri güncellenecek (K34). |
| 14 Kas | Hekim kaydı ve ortak bilgileri sadece zincir yöneticisi; şube yöneticisi atama/çıkarma ve asistan hesabı. Şube saati daraltılırken dışarıda kalan müsaitlik kontrolü (K24). |
