using System.ComponentModel.DataAnnotations;
using DisKlinigi.Core.Accounts;

namespace DisKlinigi.Web.Models;

/// <summary>
/// Giriş sayfası: tasarımdaki gibi giriş ve hasta kaydı formları yan yana (ekran 1).
/// İki form ayrı adrese gönderiliyor; hata olunca aynı sayfa, gönderilen form dolu olarak açılıyor.
/// </summary>
public sealed class AccountPageViewModel
{
    public LoginViewModel Login { get; init; } = new();
    public RegisterViewModel Register { get; init; } = new();
    public string? ReturnUrl { get; init; }
}

/// <remarks>
/// Buradaki doğrulama öznitelikleri sadece boş alanları ve biçimi yakalıyor (tarayıcıda da
/// çalışıyor). Asıl kurallar Core'da: parola kuralı <see cref="AccountService"/>, ad ve e-posta
/// <c>User</c> sınıfında. Ekrandaki kontroller atlansa bile o kurallar yine çalışıyor.
/// </remarks>
public sealed class LoginViewModel
{
    [Display(Name = "E-posta")]
    [Required(ErrorMessage = "E-posta adresinizi girin.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    public string Email { get; set; } = "";

    [Display(Name = "Parola")]
    [Required(ErrorMessage = "Parolanızı girin.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public sealed class RegisterViewModel
{
    [Display(Name = "Ad soyad")]
    [Required(ErrorMessage = "Adınızı ve soyadınızı girin.")]
    [StringLength(100, ErrorMessage = "Ad soyad en fazla {1} karakter olabilir.")]
    public string FullName { get; set; } = "";

    [Display(Name = "E-posta")]
    [Required(ErrorMessage = "E-posta adresinizi girin.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [StringLength(254)]
    public string Email { get; set; } = "";

    [Display(Name = "Cep telefonu")]
    [Phone(ErrorMessage = "Geçerli bir telefon numarası girin.")]
    [StringLength(20, ErrorMessage = "Telefon numarası en fazla {1} karakter olabilir.")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Parola")]
    [Required(ErrorMessage = "Bir parola belirleyin.")]
    [StringLength(AccountService.MaximumPasswordLength, MinimumLength = AccountService.MinimumPasswordLength,
        ErrorMessage = "Parola en az {2}, en fazla {1} karakter olmalıdır.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "Parola (tekrar)")]
    [Required(ErrorMessage = "Parolayı tekrar girin.")]
    [Compare(nameof(Password), ErrorMessage = "Parolalar aynı değil.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}

/// <summary>Henüz kodlanmamış ekranlar için geçici sayfa (bkz. <c>ComingSoon.cshtml</c>).</summary>
public sealed record ComingSoonViewModel(string Title, string PlannedDay, string Description);
