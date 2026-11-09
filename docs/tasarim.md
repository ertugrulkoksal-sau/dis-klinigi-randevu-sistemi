# Tasarım Dokümanı

Bu doküman projenin alan modelini, iş kurallarını ve verdiğim tasarım kararlarını
gerekçeleriyle anlatıyor. Kod ilerledikçe güncelleniyor; son hali README'deki
UML diyagramları ve SOLID tablosuyla birlikte okunmalı.

## 1. Alan: çok şubeli diş kliniği zinciri

Ödevdeki "hastane/klinik randevu sistemi"ni bir diş kliniği zincirine uyarladım.
Diş kliniği seçmemin sebebi, genel hastaneye göre kaynakların daha somut olması:
bir randevu sadece hekimi değil, şubedeki bir **ünit**i (diş koltuğu) de meşgul ediyor.
Bu da çakışma kontrolünü "aynı hekim aynı saatte iki yerde olamaz"dan daha ilginç hale getiriyor.

### 1.1 Kavramlar

| Kavram | Açıklama |
|---|---|
| **Şube** | Zincirin bir kliniği. Adı, adresi, haftalık çalışma saatleri, ünit sayısı ve barındırdığı bölümler var. Tek klinikli bir kurum, tek şubeli bir zincir olarak aynı modelle temsil ediliyor. |
| **Çalışma saatleri** | Şubenin haftanın her günü için açılış/kapanış saati. Kapalı günler kayıt olarak tutulmuyor. |
| **Ünit** | Diş koltuğu. Şubede kaç ünit varsa aynı anda en fazla o kadar randevu yürüyebilir. |
| **Bölüm** | Diş hekimliği dalı. Zincir genelinde bir katalog var; her şube bunlardan hangilerini barındırdığını seçiyor. |
| **Hizmet** | Bir bölüme ait tedavi. Süresi (dakika), ücreti (₺) ve onay gerektirip gerektirmediği tanımlı. |
| **Hekim** | Uzmanlık alanı (bir bölüm), verdiği hizmetler ve çalıştığı şubeler tanımlı. Bir hekim birden fazla şubede çalışabilir (ör. Salı Serdivan, Perşembe Kadıköy). |
| **Asistan** | Sağlık personeli. Bir şubeye bağlı; o şubenin onay kuyruğunu yönetir. |
| **Müsaitlik aralığı** | Hekimin belirli bir şubede, haftanın belirli bir günü çalıştığı saat aralığı (ör. Salı 09:00–13:00, Serdivan). Hastalar sadece bu aralıklara randevu alabilir. |
| **Randevu** | Hasta + hekim + hizmet + şube + başlangıç/bitiş. Oluşturulduğu andaki hizmet adı, süre ve ücret randevuya kopyalanıyor (bkz. K9). |

### 1.2 Bölümler (örnek veri)

Genel Diş Hekimliği, Ağız Diş ve Çene Cerrahisi, Endodonti, Ortodonti, Pedodonti
(çocuk diş hekimliği), Periodontoloji (diş eti), Protetik Diş Tedavisi, Restoratif Diş Tedavisi.

### 1.3 Hizmetler (örnek veri)

Ücretler gerçek tarifeden alınmadı, sadece örnek.

| Hizmet | Bölüm | Süre | Ücret | Onay |
|---|---|---|---|---|
| Muayene | Genel Diş Hekimliği | 20 dk | 750 ₺ | Otomatik |
| Kontrol | Genel Diş Hekimliği | 15 dk | 500 ₺ | Otomatik |
| Diş taşı temizliği | Periodontoloji | 30 dk | 1.500 ₺ | Otomatik |
| Kompozit dolgu | Restoratif | 45 dk | 2.500 ₺ | Hekim/asistan onayı |
| Kanal tedavisi | Endodonti | 60 dk | 5.000 ₺ | Hekim/asistan onayı |
| Diş çekimi | Cerrahi | 30 dk | 2.000 ₺ | Hekim/asistan onayı |
| İmplant uygulaması | Cerrahi | 90 dk | 25.000 ₺ | Hekim/asistan onayı |
| Ortodonti muayenesi | Ortodonti | 30 dk | 1.000 ₺ | Otomatik |
| Çocuk diş muayenesi | Pedodonti | 20 dk | 750 ₺ | Otomatik |
| Zirkonyum kron | Protetik | 60 dk | 8.000 ₺ | Hekim/asistan onayı |

### 1.4 Roller ve yetkileri

| Rol | Neler yapabilir |
|---|---|
| **Hasta** | Kayıt olur, şube → bölüm → hizmet → hekim → saat seçerek randevu alır, randevularını görür, 24 saatten fazla varsa iptal eder. |
| **Hekim** | Kendi müsaitlik aralıklarını tanımlar, kendi randevularını onaylar/reddeder, randevuyu tamamlandı/gelmedi olarak işaretler. |
| **Asistan** (sağlık personeli) | Bağlı olduğu şubenin onay kuyruğunu yönetir, şubedeki randevuları iptal edebilir. |
| **Şube yöneticisi** | Sadece kendi şubesinin çalışma saatlerini, bölümlerini ve ünit sayısını yönetir; şubesine hekim atar ya da çıkarır, şubesi için asistan hesabı açar. Randevu onaylamaz ve iptal etmez (K34). |
| **Zincir yöneticisi** | Şube açar, hizmet kataloğunu yönetir, hekim kaydını ve hekimin şubeden bağımsız bilgilerini (uzmanlık, verdiği hizmetler, giriş e-postası) düzenler; tüm şubelerde şube yöneticisinin yapabildiği her şeyi yapar. |

Ödevdeki "Hastane/Klinik Yöneticisi" rolü iki seviyeye bölündü (bkz. K3).

## 2. Randevu yaşam döngüsü

```mermaid
stateDiagram-v2
    [*] --> OnayBekliyor : onay gerektiren hizmet
    [*] --> Onaylandi : otomatik onaylı hizmet
    OnayBekliyor --> Onaylandi : hekim/asistan onaylar
    OnayBekliyor --> Reddedildi : hekim/asistan reddeder
    OnayBekliyor --> IptalEdildi : hasta veya personel iptal eder
    Onaylandi --> IptalEdildi : iptal (hasta için 24 saat kuralı)
    Onaylandi --> Tamamlandi : randevu saati geldikten sonra
    Onaylandi --> Gelmedi : randevu saati geldikten sonra
    Reddedildi --> [*]
    IptalEdildi --> [*]
    Tamamlandi --> [*]
    Gelmedi --> [*]
```

Geçerli olmayan her geçiş (ör. onaylanmış randevuyu tekrar onaylamak, reddedilmiş
randevuyu iptal etmek, saati gelmemiş randevuyu "tamamlandı" yapmak) `DomainException`
fırlatıyor ve birim testleriyle sınanıyor.

**İptal kuralı:** Hasta, randevusuna 24 saatten fazla varsa kendisi iptal edebilir.
Daha geç iptali sadece klinik personeli (hekim, asistan, yönetici) yapabilir; çünkü
son dakika iptallerinde boşalan ünitin başka hastaya verilmesi personelin işi.

**Saati gelen randevu:** Başlama anından itibaren randevu iptal edilemiyor ve onaylanamıyor.
Onaylı randevu Tamamlandı ya da Gelmedi olarak, hâlâ onay bekleyen randevu ise ret ile
kapatılıyor (K28).

## 3. Çakışma kontrolü (sistemin asıl iş kuralı)

Randevu oluşturulurken aşağıdaki kuralların **hepsi** kontrol ediliyor ve çiğnenen
kuralların tamamı kullanıcıya birlikte gösteriliyor (ilk hatada durmak yerine).
Aynı kurallar, hastaya gösterilecek boş saatleri üretirken de kullanılıyor; yani
"ekranda boş görünen saat" ile "kaydederken kabul edilen saat" aynı koddan çıkıyor.

| # | Kural | Sınıf | Açıklama |
|---|---|---|---|
| 1 | Geçmişe randevu yok | `FutureStartRule` | Başlangıç şu andan sonra olmalı (tam şu an da kabul edilmiyor). |
| 2a | Hekim bu hizmeti veriyor | `DentistOffersServiceRule` | Seçilen hizmet hekimin hizmet listesinde olmalı. |
| 2b | Şube bu bölümü barındırıyor | `BranchHostsDepartmentRule` | Hizmetin bölümü şubede bulunmalı (K33). |
| 3 | Şube çalışma saati | `WorkingHoursRule` | Randevu, şubenin o günkü açılış–kapanış aralığına tamamen sığmalı. |
| 4 | Hekim müsaitliği | `DentistAvailabilityRule` | Randevu, hekimin **o şubedeki** o günkü müsaitlik aralıklarından birine tamamen sığmalı. |
| 5 | Hekim çakışması | `DentistOverlapRule` | Hekimin **herhangi bir şubede** bu saatle kesişen aktif randevusu olmamalı. |
| 6 | Hasta çakışması | `PatientOverlapRule` | Hastanın herhangi bir şubede bu saatle kesişen aktif randevusu olmamalı. |
| 7 | Ünit kapasitesi | `ChairCapacityRule` | Randevu süresi boyunca şubede aynı anda yürüyen randevu sayısı ünit sayısını aşmamalı. |

Kuralların hepsi `IBookingRule` arayüzünü uyguluyor ve `AppointmentScheduler` tarafından
tablodaki sırayla çalıştırılıyor; mesajlar da bu sırayla gösteriliyor.

"Aktif randevu" = Onay Bekliyor veya Onaylandı durumundaki randevu (bkz. K5).

**Zaman aralıkları yarı açık:** `[başlangıç, bitiş)`. 10:00–10:30 ve 10:30–11:00
randevuları çakışmıyor; arka arkaya randevu verilebiliyor.

**Ünit kapasitesinde dikkat edilen nokta:** Sadece "kesişen randevu sayısı < ünit sayısı"
demek yanlış sonuç veriyor. Örnek: 2 ünitli şubede 09:00–09:30 ve 09:30–10:00
randevuları var; 09:00–10:00 için yeni randevu istendiğinde iki randevuyla da kesişiyor
ama aynı anda en fazla **bir** randevu yürüyor, yani bir ünit hep boş. Bu yüzden istenen
aralık içindeki **en yüksek eş zamanlı randevu sayısı** hesaplanıyor (başlangıç/bitiş
noktaları sıralanıp taranarak, `ChairCapacityRule.PeakOccupancy`). Aynı anda biten ve
başlayan iki randevu çakışmadığı için taramada aynı andaki olaylardan önce bitişler sayılıyor.
Bu örnek ve "merdiven" şeklindeki üçlü kesişme için ayrı birim testleri var
(`ChairCapacityRuleTests`).

## 4. Tasarım kararları

Her kararda neyi seçtiğimi, neden seçtiğimi ve neyi bilerek seçmediğimi yazdım.

### Alan kararları

**K1 – Çakışma kaynakları: hekim, hasta ve ünit.**
Hekim ve hasta çakışması her randevu sisteminde var; ünit kapasitesini diş kliniğine özgü
olduğu için ekledim. *Alternatif:* sterilizasyon/temizlik payı (her tedaviden sonra
ünitin 10–15 dk kapalı kalması). Gerçekçi ama zaman hesabını ve testleri karmaşıklaştırdığı
için kapsam dışı bıraktım; kural yapısı (K12) sayesinde sonradan yeni bir kural sınıfı
olarak eklenebilir.

**K2 – Onay hizmete göre belirleniyor.**
Muayene, kontrol gibi kısa ve riski düşük hizmetler otomatik onaylanıyor; kanal, implant
gibi tedaviler hekimin hastayı ve süreyi değerlendirmesi gerektiği için "Onay Bekliyor"da
kalıyor. Bunu hizmette bir `RequiresApproval` alanı ile tuttum. *Alternatif:* `Service`
sınıfından `ExaminationService` / `TreatmentService` gibi alt sınıflar türetmek. Bu iki
türün tek davranış farkı onay olduğu için kalıtım burada gereksiz karmaşıklık olurdu;
kalıtımı davranışın gerçekten farklılaştığı yerlerde (kullanıcılar, randevu durumları) kullandım.

**K3 – İki seviyeli yönetici: zincir yöneticisi ve şube yöneticisi.**
Ödevdeki "yetki çerçevesinde tanımlanabilir" ifadesini böyle karşıladım: şube yöneticisi
yalnızca kendi şubesini yönetebiliyor. Tek klinikli kurumda zincir yöneticisi tek şubeyi
yönetir, şube yöneticisi tanımlamaya gerek kalmaz. *Alternatif:* tek yönetici rolü;
basit ama yetki sınırı olmadığı için maddeyi zayıf karşılıyor.

**K4 – Hasta için 24 saat iptal kuralı.** Bkz. bölüm 2. Zamana bağlı bir kural olduğu
için test edilebilmesi adına zamanı `TimeProvider` ile alıyorum (K16).

**K5 – Onay bekleyen randevu da saati tutuyor.**
Bekleyen randevular çakışma kontrolünde sayılmazsa aynı saate birden fazla bekleyen randevu
oluşabilir ve onaylayan kişi hangisini seçeceğine karar vermek zorunda kalır. Hasta da
"aldım" sandığı saati kaybedebilir. Reddedilen ya da iptal edilen randevu saati hemen
serbest bırakıyor.

**K6 – Ünitler sayı olarak tutuluyor, tek tek varlık olarak değil.**
Kapasite kontrolü için şubedeki ünit sayısı yeterli. *Alternatif:* her ünit ayrı kayıt ve
randevuya belirli bir ünit atanması. Ünit bazlı ekipman farkı (ör. sadece bir ünitte
röntgen var) olsaydı gerekirdi; bu projede yok.

**K7 – Tek kurum, çok şube (çok kiracılı değil).**
Sistem tek bir zincir için kuruluyor. Birden fazla bağımsız kurumu aynı veritabanında
barındırmak (multi-tenant) ödevin istediği bir şey değil ve her sorguya kurum filtresi
eklemeyi gerektirirdi.

**K8 – Müsaitlik haftalık tekrar eden aralıklar olarak ve şube bazında tutuluyor.**
Hekimlerin çalışma düzeni genelde haftalık sabit. Aralığın şubeye bağlı olması, bir hekimin
farklı günlerde farklı şubelerde çalışmasını mümkün kılıyor ve hekim çakışmasının şubeler
arası kontrol edilmesini anlamlı yapıyor. *Alternatif:* tarih bazlı tek tek aralıklar;
esnek ama hekimin her hafta yeniden girmesi gerekir. İzin günleri ileride ayrı bir
"müsait değil" kaydıyla eklenebilir.

**K9 – Randevu oluşturulduğu andaki süre ve ücreti saklıyor.**
Ödev randevuda hizmet, süre, ücret, personel ve tarih/saatin kaydedilmesini istiyor.
Bunları sadece hizmete referans olarak tutsaydım, yönetici hizmet ücretini değiştirdiğinde
geçmiş ve gelecek randevuların ücreti de değişmiş gibi görünürdü. Bu yüzden randevuya
o anki değerlerin bir kopyası yazılıyor.

**K10 – Saatler yerel saat olarak tutuluyor.**
Tüm şubeler Türkiye'de ve Türkiye'de yaz saati uygulaması yok, bu yüzden saat dilimi
dönüşümüne gerek görmedim. SQLite `DateTimeOffset` üzerinde sıralama ve karşılaştırmayı
iyi desteklemediği için `DateTime` kullanılıyor.

### Teknik kararlar

**K11 – Katmanlar: Core, Web, Tests.**
`DisKlinigi.Core` alan sınıflarını, iş kurallarını ve arayüzleri içeriyor; hiçbir altyapı
paketine bağımlı değil. `DisKlinigi.Web` MVC arayüzünü ve EF Core ile veri erişimini içeriyor.
`DisKlinigi.Tests` sadece Core'u test ediyor. Böylece çakışma kuralları veritabanı ve web
sunucusu olmadan, milisaniyeler içinde test edilebiliyor. *Alternatif:* ayrı bir
Infrastructure projesi (klasik Clean Architecture). Bu boyutta bir projede veri erişimini
Web içinde bir klasörde tutmak yeterli; ayrı proje sadece tören eklerdi.

**K12 – Çakışma kuralları Strategy deseniyle ayrı sınıflar.**
Her kural `IBookingRule` arayüzünü uygulayan ayrı bir sınıf. Randevu planlayıcı
(`AppointmentScheduler`) kuralların listesini DI ile alıp hepsini sırayla çalıştırıyor;
hangi kuralın ne yaptığını bilmiyor. Yeni kural eklemek mevcut kodu değiştirmeden yeni
bir sınıf eklemek demek (Open/Closed). Her kural tek başına test edilebiliyor.
*Alternatif:* tek bir büyük `Validate()` metodu; başta daha kısa ama kural sayısı arttıkça
okunmaz ve test edilmesi zor hale gelir.

**K13 – Randevu durumları State deseniyle.**
Her durum (`PendingState`, `ConfirmedState`, ...) hangi işlemlere izin verdiğini kendisi
biliyor; izin verilmeyen işlem temel sınıfta varsayılan olarak hata fırlatıyor. Veritabanında
durum bir enum olarak saklanıyor, nesne yüklenince ilgili durum sınıfı seçiliyor.
*Alternatif:* `(durum, işlem) → yeni durum` şeklinde bir geçiş tablosu. Geçişlerde sadece
durum değişseydi tablo daha basit olurdu; ama bazı geçişlerin koşulu var (tamamlandı için
randevu saatinin gelmiş olması, hasta iptali için 24 saat), bu koşulları ilgili durumun
içinde tutmak daha okunur.

**K14 – Kullanıcılar kalıtım hiyerarşisi, EF Core'da tek tablo (TPH).**
`User` soyut sınıfından `Patient`, `Dentist`, `Assistant`, `BranchManager`,
`ChainAdministrator` türüyor. Yetki soruları (`CanApprove(randevu)`, `CanCancel(randevu, şimdi)`,
`CanManageBranch(şube)`) her sınıfta farklı cevaplanıyor: hekim sadece kendi randevusunu,
asistan kendi şubesinin randevusunu onaylayabiliyor; hasta ise hiç onaylayamıyor. Controller'da
`if (rol == ...)` zincirleri yerine bu çok biçimli metotlar çağrılıyor. Tek tablo seçmemin
sebebi giriş sırasında e-postaya göre tek sorguyla kullanıcıyı bulabilmek.
*Alternatif:* tek `User` sınıfı ve rol alanı; ama o zaman rol kontrolleri kod geneline dağılır.

**K15 – ASP.NET Core Identity yerine çerez kimlik doğrulaması + `PasswordHasher`.**
Identity kendi kullanıcı tablolarını ve hazır ekranlarını getiriyor; bu hem K14'teki
hiyerarşiyle çakışıyor hem de kimlik doğrulamanın nasıl çalıştığını gizliyor. Bunun yerine
ASP.NET Core'un çerez kimlik doğrulamasını kullanıp parolaları framework'ün `PasswordHasher`
sınıfıyla (PBKDF2, tuzlu) saklıyorum. Parolalar hiçbir yerde düz metin tutulmuyor. Core'da
`IPasswordHasher` adında kendi arayüzüm var, framework sınıfı Web'de bunun arkasına saklanıyor.

**K16 – Zaman `TimeProvider` ile alınıyor.**
.NET'in hazır `TimeProvider` soyutlaması kullanılıyor; testlerde `FakeTimeProvider` ile
"şu an" sabitlenip 24 saat kuralı ve geçmişe randevu kuralı deterministik test ediliyor.
Kendi `IClock` arayüzümü yazmak yerine standart olanı tercih ettim.

**K17 – Veri erişimi repository arayüzleri üzerinden.**
EF Core'un `DbContext`'i zaten bir unit of work, bu yüzden her tablo için genel amaçlı
(`IRepository<T>`) bir katman yazmadım. Sadece iş kurallarının ihtiyaç duyduğu sorgular için
amaca özel arayüzler var (ör. "hekimin bu gün aktif randevuları"). Core bu arayüzleri tanımlıyor,
Web EF Core ile gerçekleştiriyor (Dependency Inversion). Testlerde bellek içi sahte sınıflar kullanılıyor.

**K18 – Migration + açılışta otomatik örnek veri.**
Şema EF Core migration'larıyla yönetiliyor; uygulama açılırken bekleyen migration'lar
uygulanıyor ve veritabanı boşsa örnek veri yükleniyor. Sunum makinesinde `dotnet run`
dışında bir şey yapmaya gerek kalmıyor. *Alternatif:* `EnsureCreated()`; daha basit ama
model değiştiğinde veritabanını silmek gerekiyor ve şema geçmişi tutulmuyor.

**K19 – Eş zamanlı randevu denemeleri: kontrol ve kayıt, yazma kilidi alan tek bir işlemde.**
İki hasta aynı anda aynı saate randevu almaya çalışırsa ikisi de "boş" görüp kaydedebilir.
Bunu önlemek için `AppointmentScheduler.BookAsync` önce işlemi açıyor, günün randevularını
işlem **içinde** okuyor, kuralları çalıştırıyor ve kaydı aynı işlemde onaylıyor. İşlem
Core'da `IAppointmentRepository.BeginBookingTransactionAsync` ile soyutlandı; EF Core
gerçekleştirimi (10 Kasım) `Database.BeginTransactionAsync` kullanacak. SQLite'ta bu işlem
`BEGIN IMMEDIATE` ile başlıyor, yani yazma kilidi işlemin başında alınıyor: ikinci istek
birincinin bitmesini bekliyor, sonra kontrolünü güncel veriyle yapıyor ve "hekim dolu" ya da
"ünitler dolu" cevabı alıyor. Testte bellek içi repository aynı davranışı bir semaforla
taklit ediyor; okumayı işlemin dışına aldığımda iki eş zamanlılık testinin de düştüğünü denedim.
*Bedeli:* bütün randevu kayıtları (farklı şubelerde bile) sırayla yapılıyor. Bir klinik
zincirinde saniyede birkaç randevu bile alınmadığı için bu bir sorun değil; okuma (boş saat
listesi) kilit almıyor.
*Alternatifler:* (a) Uygulama içinde kilit (`SemaphoreSlim`): veritabanından bağımsız ama
sadece tek süreçte işe yarıyor, uygulama iki örnekle çalışırsa koruma kalmıyor. (b) Benzersiz
indeks: "aynı hekim + aynı başlangıç" gibi bir indeks, kesişen ama başlangıcı farklı randevuları
(10:00–11:00 ile 10:30–11:00) yakalayamıyor; ünit kapasitesini hiç ifade edemiyor. (c) İyimser
eş zamanlılık (satır sürümü): çakışma tek bir satırda değil bir zaman aralığında olduğu için
hangi satırın sürümüne bakılacağı belli değil; "hekimin günü" gibi yapay bir satır eklemek
gerekirdi. Sunucu tabanlı bir veritabanına geçilirse aynı işlem `Serializable` yalıtım
seviyesiyle ya da hekim/şube satırına kilit alarak korunabilir; planlayıcının kodu değişmez.

**K20 – Kod İngilizce, arayüz ve yorumlar Türkçe.**
Sınıf ve metot adlarında Türkçe karakter kullanmak (ı, ş, ğ) hem yazımı zorlaştırıyor hem
de framework adlarıyla karışık bir dil ortaya çıkarıyor. Kullanıcının gördüğü her şey ve
kod yorumları Türkçe.

**K21 – Yerel çalıştırma http üzerinden.**
macOS'te https geliştirme sertifikası ayrıca güvenilir yapılmayı gerektiriyor
(`dotnet dev-certs https --trust`). Sunumda sorun çıkmasın diye uygulama `http://localhost:5080`
adresinde açılıyor.

### Alan modelini kodlarken verilen kararlar (7 Kasım)

**K22 – İki ayrı zaman aralığı tipi: `TimeRange` ve `TimeOfDayRange`.**
`TimeRange` takvimde belirli bir aralık (10 Kasım 11:00–12:00), `TimeOfDayRange` ise
tarihten bağımsız, her hafta tekrar eden bir saat kalıbı (Salı 09:00–13:00). İkisi de
değiştirilemez değer nesnesi (record), ikisi de yarı açık ve geçersiz hali (bitiş ≤ başlangıç)
oluşturulamıyor. Kalıp, `On(tarih)` ile belirli bir güne yerleştirilip takvimdeki aralıkla
karşılaştırılıyor; şube saati ve hekim müsaitliği kontrolleri aynı `Covers` metodunu kullanıyor.
*Alternatif:* tek tip ve "günün başından itibaren dakika" gibi sayılar. Daha az sınıf olurdu
ama "bu sayı tarih mi saat mi?" karışıklığı her yerde kontrol gerektirirdi.

**K23 – "Aynı kayıt mı?" sorusu `Entity.IsSameAs` ile soruluyor.**
Şubenin bölüm listesi, hekimin şubeleri ve şube yöneticisinin yetkisi gibi yerlerde iki
nesnenin aynı kayıt olup olmadığını bilmek gerekiyor. Önce referansa, Id atanmışsa Id'ye
bakılıyor; kaydedilmemiş iki nesnenin Id'si 0 olduğu için sadece Id karşılaştırması yanlış
sonuç verirdi. *Alternatif:* `Equals`/`GetHashCode`'u ezmek. Id kayıttan sonra değiştiği
için hash değeri de değişir ve nesne bir `HashSet` içindeyse bulunamaz hale gelir; bu yüzden
açık isimli ayrı bir metot tercih ettim.

**K24 – Müsaitlik aralığı eklemenin kuralları hekim sınıfında.**
Aralık sadece `Dentist.AddAvailability` ile eklenebiliyor ve üç şey kontrol ediliyor:
hekim o şubeye atanmış mı, aralık şubenin o günkü çalışma saatine tamamen sığıyor mu,
hekimin aynı gündeki başka bir aralığıyla (hangi şubede olursa olsun) kesişiyor mu.
Uç uca aralıklar serbest (sabah Serdivan 09:00–13:00, öğleden sonra Kadıköy 13:00–18:00);
şubeler arası yol süresi hesaba katılmıyor. Uç uca aralıklar birleştirilmiyor, bu yüzden
iki aralığa bölünen bir randevu (12:30–13:30) kabul edilmiyor. Müsaitlik aralığı olan
şubeden hekim çıkarılamıyor. Şube çalışma saati sonradan daraltılırsa dışarıda kalan
aralıklar şube sınıfının göremediği bir şey; bu kontrol yönetici paneli gününde (14 Kasım)
servis katmanında yapılacak.

**K25 – Hizmet süresi 5 dakikanın katı, en fazla 240 dakika.**
Boş saatler çeyrek saatlik adımlarla üretilecek (arayüz tasarımında önerildi), bu yüzden
daha ince süreye gerek yok; 4 saatten uzun işlemler gerçekte birkaç seansa bölünüyor.
Ücret negatif olamıyor, ücretsiz (0 ₺) hizmet tanımlanabiliyor.

**Not:** `CanApprove` ve `CanCancel` yetki metotları randevu sınıfına ihtiyaç duyduğu için
8 Kasım'da randevu durumlarıyla birlikte eklendi (K27). 7 ve 8 Kasım'da eklenen alan sınıfları
(çalışma saatleri, bölümler, hizmetler, kullanıcılar, randevular) henüz veritabanına eşlenmedi;
`AppDbContext` bunları 10 Kasım'daki veritabanı adımına kadar görmezden geliyor, bu yüzden
bu iki günde migration yok.

### Randevu durumlarını kodlarken verilen kararlar (8 Kasım)

**K26 – Durum enum olarak saklanıyor, durum nesnesi her seferinde enum'dan seçiliyor.**
Durum sınıflarının kendi verisi yok; her durumdan tek bir örnek var. Randevu sadece
`AppointmentStatus` değerini tutuyor, işlem gelince `AppointmentState.For(Status)` ile ilgili
durum nesnesi seçiliyor. Böylece veritabanından okunan randevuda "enum bir şey, durum nesnesi
başka bir şey söylüyor" gibi bir tutarsızlık oluşamıyor ve EF Core'un durum nesnesinden haberi
olması gerekmiyor. Durum nesnesindeki işlem metotları `internal`: randevuyu atlayıp durumu
doğrudan değiştirmek mümkün değil. Onay bekliyor ve Onaylandı ortak bir `ActiveState`
sınıfından türüyor, çünkü ikisinde de iptal aynı kurallarla yapılıyor ve ikisi de saati tutuyor.
Dört son durum (Reddedildi, İptal edildi, Tamamlandı, Gelmedi) davranış olarak aynı olduğu için
dört boş alt sınıf yerine tek `FinalState` sınıfının dört örneği.
*Alternatif:* durum nesnesini randevunun içinde alan olarak tutmak; ama o zaman enum ile
nesnenin her değişiklikte birlikte güncellenmesi gerekirdi.

**K27 – Onaylama, reddetme ve sonucu (tamamlandı/gelmedi) işleme aynı yetki: `CanApprove`.**
Üç işlemi de aynı kişiler yapıyor: randevunun hekimi ve şubenin asistanı. Ayrı bir
`CanRecordOutcome` metodu açsaydım her rolde aynı cevabı veren iki metot olurdu.
Yöneticiler (şube ve zincir) randevu onaylamıyor: onay, hastanın ve tedavi süresinin
değerlendirilmesi demek (K2), bu da tıbbi bir karar. 9 Kasım'daki tasarım incelemesinden sonra
yöneticilerin randevu iptali de kaldırıldı (K34). Hekim yetkisi şubeye değil randevuya bağlı:
aynı şubedeki başka bir hekim, meslektaşının randevusunu onaylayamıyor.
Kullanıcı sınıfları sadece "bu kişi bu randevuyla ilgili mi" sorusunu cevaplıyor; durumun
işleme izin verip vermediğini durum sınıfı ayrıca kontrol ediyor. Ekranda bir butonun
görünmesi için ikisinin de evet demesi gerekecek.

**K28 – Saati gelmiş randevu iptal edilemiyor ve onaylanamıyor.**
Başlamış bir randevuyu iptal etmek geçmişi değiştirmek olur; onaylı randevu Tamamlandı ya da
Gelmedi ile kapatılıyor. Onay bekleyen randevunun saati onaylanmadan geçerse onaylamanın
anlamı kalmıyor; bu randevu ret ile kapatılıyor (ret zaman koşulu olmayan tek işlem).
Tamamlandı/Gelmedi başlama anından itibaren (başlama anı dahil) işaretlenebiliyor;
randevunun bitmesi beklenmiyor, çünkü hekim sonucu genelde hasta koltuktayken işliyor.
*Alternatif:* saati geçen bekleyen randevuları otomatik olarak reddetmek. Bunun için arka planda
çalışan bir iş gerekirdi; şimdilik onay kuyruğunda "saati geçti" olarak gösterilip elle
kapatılması yeterli.

**K29 – 24 saat kuralı onay bekleyen randevuda da geçerli; sınır "24 saatten fazla".**
Durum diyagramında 24 saat kuralı onaylı randevunun okunda yazıyordu; ama bekleyen randevu da
saati ve üniti tutuyor (K5), son dakika iptalinde boşalan saati yönetmek yine şubenin işi.
Bu yüzden kural duruma bakmadan uygulanıyor. Tam 24 saat kala hasta artık iptal edemiyor
("24 saatten fazla varsa" ifadesine göre); bu sınır ayrı bir testle sınanıyor.

**K30 – Randevu oluşturulurken sadece kendi tutarlılığı kontrol ediliyor.**
`Appointment` yapıcısı hasta, hekim, hizmet ve şubenin verilmesini istiyor, bitişi hizmet
süresinden hesaplıyor ve ilk durumu hizmete göre seçiyor. Başka randevulara ya da hekimin
takvimine bakan kurallar (bölüm 3'teki kurallar) tek bir randevunun bilebileceği şeyler
değil; onlar `IBookingRule` sınıflarında ve `AppointmentScheduler` randevuyu oluşturmadan önce
kontrol ediliyor (9 Kasım). Yapıcıyı `internal` yapıp randevuyu sadece planlayıcının
oluşturmasını sağlamayı düşündüm; ama örnek veride geçmiş tarihli randevular da gerekecek
(10 Kasım) ve bunlar "geçmişe randevu yok" kuralından geçemez. Bu yüzden yapıcı açık, kuralları
atlamamak uygulama katmanında planlayıcıyı kullanmakla sağlanıyor.

**Ret gerekçesi** isteğe bağlı olarak saklanıyor; arayüz tasarımındaki 3. küçük karar
("reddederken gerekçe yazılıyor") onaylanırsa ekranda zorunlu alan yapılacak.

### Çakışma kontrolünü kodlarken verilen kararlar (9 Kasım)

**K31 – Kurallar veritabanına gitmiyor; planlayıcı günün randevularını bir kez okuyup
`BookingContext` ile veriyor.**
Bağlamda "şu an" ve o günle kesişen, ilgili hekime, hastaya ya da şubeye ait aktif randevular
var. Sadece aktif randevuların tutulması (K5) bağlamın işi; kuralların her biri "iptal edileni
sayma" diye ayrıca düşünmüyor. Böylece kurallar saf fonksiyon gibi çalışıyor ve testleri için
sadece bir randevu listesi yetiyor. Boş saat üretiminde bir gün için onlarca aday saat tek
sorguyla kontrol ediliyor. Kural çiğnendiğinde istisna değil `RuleResult` dönüyor, çünkü ilk
hatada durmak yerine çiğnenen kuralların hepsi gösteriliyor ve dolu saat olağan bir cevap.
*Alternatif:* her kural kendi repository sorgusunu yapsın. Kurallar daha bağımsız olurdu ama
boş saat listesinde her aday saat için üç ayrı sorgu gerekirdi ve kural testleri sahte
repository'ye ihtiyaç duyardı.

**K32 – Boş saatler müsaitlik aralığının başından itibaren 15 dakikalık adımlarla üretiliyor.**
Hekimin o gün o şubedeki her aralığı için aralığın başından başlanıp hizmet aralığın sonuna
sığdığı sürece 15'er dakika ilerleniyor; her aday saat kayıttaki kuralların aynısından geçiyor.
Hastanın kendi başka randevusuyla çakışan saat de gösterilmiyor. Örneğin 20 dakikalık muayeneden
sonra 09:20–09:30 arası boş kalıyor; bunu kabul ettim, çünkü liste düzenli ve okunaklı oluyor.
Kayıt sırasında saatin ızgarada olması ayrıca kontrol edilmiyor; ekranda sadece listedeki
saatler seçilebiliyor. *Alternatif:* var olan randevuların bitiş saatlerini de aday yapmak
(09:20'ye randevu vermek). Ünitler daha verimli kullanılır ama liste 09:00, 09:15, 09:20, 09:35…
gibi düzensiz görünür.

**K33 – "Hekim bu hizmeti veriyor" kuralı ikiye ayrıldı.**
Bölüm 3'teki 2. kural iki ayrı soru soruyordu: hekim bu hizmeti veriyor mu, şube bu bölümü
barındırıyor mu? İkisini ayrı sınıf yaptım (`DentistOffersServiceRule`,
`BranchHostsDepartmentRule`); her kural sınıfı tek bir soruyu cevaplıyor ve ikisi birlikte
çiğnenirse kullanıcı iki ayrı mesaj görüyor.

**Otomatik onay** planlayıcıda değil, randevunun yapıcısında: ilk durum hizmetin
`RequiresApproval` alanına göre seçiliyor (K2, K30). Planlayıcı kurallar geçince randevuyu
oluşturup kaydediyor; muayene "Onaylandı", kanal tedavisi "Onay bekliyor" olarak çıkıyor.

**DI kaydı:** Kurallar ve planlayıcı, EF Core repository'si yazılınca (10 Kasım) `Program.cs`'e
ekleniyor. Bugün eklemedim, çünkü geliştirme ortamında ASP.NET Core açılışta bütün kayıtları
doğruluyor ve gerçekleştirimi olmayan `IAppointmentRepository` uygulamanın açılmasını engellerdi.

### Arayüz tasarımı incelemesinden sonra verilen kararlar (9 Kasım)

Tasarımı çizen değil, ayrı bir inceleyici ekranları ödev maddeleri, bu doküman ve mevcut kodla
karşılaştırdı. Bulgular ve kanvasta yapılan düzeltmeler [arayuz-tasarimi.md](arayuz-tasarimi.md)
dosyasında. Alan modelini etkileyen kararlar şunlar:

**K34 – Yöneticiler randevuya dokunmuyor; şube yöneticisi hekimin ortak bilgilerini düzenlemiyor.**
İlk tasarımda şube yöneticisi bir hekimin uzmanlığını, verdiği hizmetleri ve e-postasını
değiştirebiliyordu. Ama bu bilgiler hekimin bütün şubeleri için ortak: Serdivan yöneticisinin
yaptığı değişiklik hekimin Kadıköy'deki çalışmasını da etkiliyordu, bu da K3'teki "sadece kendi
şubesi" sınırını aşmak demek. Bu yüzden hekim kaydı ve ortak bilgileri zincir yöneticisinde;
şube yöneticisi sadece hekimi kendi şubesine atıyor ya da çıkarıyor ve kendi şubesi için asistan
hesabı açıyor. Yöneticilerin şube randevularını iptal etmesi için ayrıca bir ekran gerekecekti;
kapsamı büyütmemek için bu yetkiyi kaldırdım. Randevu işlemleri hekim ve asistanda kalıyor.
Kodda `BranchManager.CanCancel` ve `ChainAdministrator.CanCancel` personel paneli gününde
(13 Kasım) `false` olacak. *Alternatif:* yöneticiye salt okunur bir "şube randevuları" listesi
ve iptal yetkisi vermek; gerçek bir klinikte faydalı ama ödevin rol tanımı bunu gerektirmiyor.

**K35 – Randevuda hasta notu ve oluşturulma zamanı tutuluyor.**
Hasta randevu alırken hekime kısa bir not yazabiliyor (en fazla 500 karakter, isteğe bağlı);
onay kuyruğunda randevunun ne zaman alındığı görünüyor. İkisi de onay veren kişinin karar
vermesine yardım ediyor. Veritabanı gününde (10 Kasım) `Appointment`'a ekleniyor.

**K36 – Personel hesabı geçici parolayla açılıyor, ilk girişte değiştirme zorunlu değil.**
Tasarım ilk halinde "ilk girişte parola değiştirme" akışı öneriyordu. Bu, ayrı bir ekran ve
kullanıcıda ek bir durum alanı demek; ödevin istediği bir şey olmadığı için kapsam dışı bıraktım.
Parolalar yine `PasswordHasher` ile saklanıyor (K15).

**Şubeler arası yol süresi (K24'teki varsayım) olduğu gibi kalıyor:** bir hekim sabah bir
şubede, öğleden sonra başka şubede uç uca çalışabiliyor. Gerçekte arada yol süresi gerekir;
bu, hekimin aralıklarını girerken kendisinin bırakacağı bir boşluk olarak düşünüldü.

## 5. İlk sınıf taslağı

Bu taslak 7–9 Kasım'da kodlanacak; README'deki son diyagram koddan çıkarılacak.

```mermaid
classDiagram
    class User {
        <<abstract>>
        +FullName
        +Email
        +PasswordHash
        +CanApprove(Appointment) bool
        +CanCancel(Appointment, DateTime) bool
        +CanManageBranch(Branch) bool
    }
    User <|-- Patient
    User <|-- Dentist
    User <|-- Assistant
    User <|-- BranchManager
    User <|-- ChainAdministrator

    class Branch {
        +Name
        +ChairCount
        +WorkingHours
        +Departments
    }
    class Department
    class Service {
        +Duration
        +Price
        +RequiresApproval
    }
    class AvailabilityWindow {
        +DayOfWeek
        +Start
        +End
    }
    class Appointment {
        +Period : TimeRange
        +PriceAtBooking
        +Status
        +IsActive
        +Approve(User, now)
        +Reject(User, reason)
        +Cancel(User, now)
        +Complete(User, now)
        +MarkNoShow(User, now)
    }
    class AppointmentState {
        <<abstract>>
    }
    AppointmentState <|-- ActiveState
    ActiveState <|-- PendingState
    ActiveState <|-- ConfirmedState
    AppointmentState <|-- FinalState

    class IBookingRule {
        <<interface>>
        +Check(BookingRequest, BookingContext) RuleResult
    }
    IBookingRule <|.. DentistOverlapRule
    IBookingRule <|.. PatientOverlapRule
    IBookingRule <|.. ChairCapacityRule
    IBookingRule <|.. DentistAvailabilityRule
    IBookingRule <|.. WorkingHoursRule

    class AppointmentScheduler {
        +BookAsync(BookingRequest)
        +GetFreeSlotsAsync(...)
    }

    Branch "*" -- "*" Department : barındırır
    Department "1" -- "*" Service
    Dentist "*" -- "*" Service : verir
    Dentist "*" -- "*" Branch : çalışır
    Dentist "1" -- "*" AvailabilityWindow
    AvailabilityWindow "*" -- "1" Branch
    Appointment "*" -- "1" Patient
    Appointment "*" -- "1" Dentist
    Appointment "*" -- "1" Service
    Appointment "*" -- "1" Branch
    Appointment --> AppointmentState
    AppointmentScheduler --> IBookingRule
```

## 6. Ekranlar

Ekranlar kod yazılmadan önce tasarım kanvasında çizildi (7 Kasım), bağımsız bir incelemeden
sonra düzeltildi (9 Kasım) ve onaya sunuldu.
Kanvas bağlantısı, ekranların ayrıntılı listesi ve onay durumu: [arayuz-tasarimi.md](arayuz-tasarimi.md).

1. Giriş ve hasta kaydı
2. Hasta: randevu alma akışı (şube → bölüm → hizmet → hekim → tarih/saat → özet ve onay)
3. Hasta: randevularım (durum, iptal)
4. Hekim/asistan: günlük takvim, onay kuyruğu, müsaitlik aralıkları
5. Yönetici: şubeler, çalışma saatleri, bölümler, hizmetler, şube personeli, hekim kaydı
