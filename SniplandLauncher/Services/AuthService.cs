using System;
using System.Threading.Tasks;
using CmlLib.Core.Auth;
using SniplandLauncher.Models;

namespace SniplandLauncher.Services
{
    public class AuthService
    {
        private const string ElyByAuthServer = "https://authserver.ely.by/auth";

        public Task<UserSession?> LoginAsync(string email, string password)
        {
            return Task.Run(() => {
                try
                {
                    var login = new MLogin();
                    var session = login.Authenticate(email, password, ElyByAuthServer);

                    if (session.Result != MLoginResult.Success)
                        return null;

                    return new UserSession
                    {
                        Username = session.Session?.Username,
                        UUID = session.Session?.UUID,
                        AccessToken = session.Session?.AccessToken,
                        ClientToken = session.Session?.ClientToken
                    };
                }
                catch
                {
                    return null;
                }
            });
        }
    }
}
