namespace MyBlazorSite.Data;

public class AppUserState
{
    public bool IsLoggedIn { get; set; }

    public string UserName { get; set; } = "Гость";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "Гость";

    // Временный ключ гостя/пользователя для привязки его заявок
    public string UserKey { get; set; } = Guid.NewGuid().ToString("N");

    public bool IsAdmin => Role == "Администратор";
    public bool IsEstimator => Role == "Оценщик";
    public bool IsGuest => !IsLoggedIn;

    public bool CanManagePrices => IsAdmin;
    public bool CanChangeRequestStatus => IsAdmin || IsEstimator;
    public bool CanSeeNotifications => IsAdmin || IsEstimator;

    // Все заявки видят только админ и оценщик
    public bool CanSeeAllRequests => IsAdmin || IsEstimator;

    public void LoginAsAdmin()
    {
        IsLoggedIn = true;
        UserName = "Администратор";
        Email = "admin@autoestimate.ru";
        Role = "Администратор";
        UserKey = Email;
    }

    public void LoginAsEstimator()
    {
        IsLoggedIn = true;
        UserName = "Оценщик";
        Email = "estimator@autoestimate.ru";
        Role = "Оценщик";
        UserKey = Email;
    }

    public void Logout()
    {
        IsLoggedIn = false;
        UserName = "Гость";
        Email = "";
        Role = "Гость";
        UserKey = Guid.NewGuid().ToString("N");
    }
}