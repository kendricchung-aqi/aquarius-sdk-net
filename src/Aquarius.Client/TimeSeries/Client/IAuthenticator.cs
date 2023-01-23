namespace Aquarius.TimeSeries.Client
{
    public interface IAuthenticator
    {
        string Login(string username, string password, string aqiIdpAccessToken);
        void Logout();
    }
}
