# Arayüz Tasarımı

Ekranları kod yazmadan önce tasarım kanvasında çizdim. Kanvas bağlantısı:

**https://claude.ai/artifact/LqSo2athR6pzshgDTkbAhc**

**Onay durumu: onay bekliyor** (7 Kasım 2026'da onaya sunuldu)

Kanvas şimdilik sadece bana açık; başkasına göstermek için sayfadaki Paylaş menüsünden paylaşmak gerekiyor.
Ekranlar onaylanana kadar koda dökülmüyor; plan gereği ekran işleri en erken 11 Kasım'da başlıyor.
Onaydan sonra yapılacak tasarım değişiklikleri de önce kanvasta gösterilip sonra koda uygulanacak.

## Ekranlar

| # | Ekran | Kim görüyor | İçerik |
|---|---|---|---|
| 1 | Giriş ve hasta kaydı | Herkes | Tek giriş formu (rol girişten sonra belirleniyor), hasta kayıt formu, hatalı giriş örneği |
| 2a | Randevu al: seçimler | Hasta | Adım göstergesi; şube → bölüm → hizmet → hekim. Her seçim sonrakini daraltıyor (şubenin bölümleri, o hizmeti o şubede veren hekimler). Onay gerektiren hizmet rozetle belirtiliyor. |
| 2b | Randevu al: tarih/saat, özet ve onay | Hasta | Hekimin o şubede çalıştığı günler, boş saatler, özet kartı (ücret, süre, onay bilgisi), hekime not, kayıt sırasında kural çiğnenirse gösterilecek hata kutusu |
| 3 | Randevularım | Hasta | Yaklaşan randevular (iptal butonu; 24 saatten az kaldıysa pasif ve nedeni yazıyor), geçmiş randevular tablosu, tüm durum rozetleri |
| 4a | Günlük takvim | Hekim, asistan | Günün randevuları zaman sırasıyla, boş aralıklar, "şu an" çizgisi; duruma göre Onayla/Reddet, Tamamlandı/Gelmedi, İptal |
| 4b | Onay kuyruğu | Hekim, asistan | Onay bekleyen randevular, hekim filtresi, ret gerekçesi girişi |
| 4c | Müsaitlik aralıkları | Hekim | Haftalık görünüm (şubeye göre renk), aralık listesi, aralık ekleme formu ve çalışma saatine sığmama hatası örneği |
| 5a | Şubeler ve çalışma saatleri | Zincir yöneticisi, şube yöneticisi | Şube listesi, şube bilgileri ve ünit sayısı, haftalık çalışma saatleri, barındırılan bölümler |
| 5b | Bölümler ve hizmetler | Zincir yöneticisi (şube yöneticisi salt okunur) | Bölüm kataloğu, hizmet tablosu (süre, ücret, onay), hizmet düzenleme ve ücret değişikliğinin eski randevuları etkilemediği uyarısı |
| 5c | Personel | Zincir yöneticisi, şube yöneticisi | Şube personeli tablosu, hekimin uzmanlığı ve verdiği hizmetler (şube yöneticisi gözünden) |

## Ortak kararlar

- **Renkler:** açık gri zemin, beyaz kartlar, ana renk petrol yeşili. Durum rozetleri hem renk hem
  yazıyla ayrışıyor (Onay bekliyor, Onaylandı, Reddedildi, İptal edildi, Tamamlandı, Gelmedi);
  sadece renge güvenmiyorum.
- **Üst menü role göre değişiyor.** Hasta: Randevu al, Randevularım. Personel: Takvim, Onay kuyruğu,
  Müsaitlik (asistanda Müsaitlik yok). Yönetici: Şubeler, Bölüm ve hizmetler, Personel.
- **Butonlar yetkiye ve duruma göre çıkıyor.** Örneğin onaylı randevuda "Onayla" yok; "Tamamlandı"
  sadece saati gelmiş onaylı randevuda var. Bu, kodda `CanApprove` / `CanCancel` ve State deseninin
  ekrandaki karşılığı.
- **Hastaya sadece boş saatler gösteriliyor.** Kaydederken aynı kurallar tekrar çalıştığı için
  arada saat dolarsa çiğnenen kuralların hepsi birlikte gösteriliyor (tasarım.md bölüm 3).
- **Telefon genişliği:** menü ve kartlar alt alta diziliyor, tablolar kendi kutusunda yatay kayıyor.
- Sayfalar Bootstrap'in hazır görünümüne yakın tutuldu, böylece kodlarken ek CSS kütüphanesi gerekmeyecek.

## Tasarımda ortaya çıkan, onayına bağlı küçük kararlar

Ekranları çizerken alan modelinde henüz olmayan birkaç ayrıntı çıktı. Tasarımda bunları şöyle gösterdim;
onaylarsan ilgili günlerde koda eklenecek:

1. Boş saatler **15 dakikada bir** başlıyor (boş saat üretimi, 9 Kasım).
2. Hasta randevu alırken **hekime kısa bir not** yazabiliyor.
3. Reddederken **gerekçe** yazılıyor ve hastaya gösteriliyor.
4. İleri tarihte randevusu olan müsaitlik aralığı **silinemiyor**; şube çalışma saati daraltılırken
   dışarıda kalan müsaitlik ya da randevu varsa kayıt engelleniyor.
5. Personel hesabı yönetici tarafından açılıyor ve **geçici parola** veriliyor.
