
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Quickstarts.ReferenceServer
{

    public class UserStore : IUserStore<IdentityUser>
    {
        private bool m_disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!m_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                m_disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        // interface implementations not shown
        public Task<IdentityResult> CreateAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IdentityResult> DeleteAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IdentityUser> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IdentityUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetNormalizedUserNameAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetUserIdAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetUserNameAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SetNormalizedUserNameAsync(IdentityUser user, string normalizedName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SetUserNameAsync(IdentityUser user, string userName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IdentityResult> UpdateAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    public interface IOperation
    {
        string OperationId { get; }
    }

    public interface ITransientOperation : IOperation
    {
    }

    public interface IScopedOperation : IOperation
    {
    }

    public interface ISingletonOperation : IOperation
    {
    }

    public class DefaultOperation :
        ITransientOperation,
        IScopedOperation,
        ISingletonOperation
    {
        public string OperationId { get; } = Guid.NewGuid().ToString();
    }

    public class UserService //: IUserService
    {
        private readonly UserManager<IdentityUser> m_userManager;
        private readonly ILogger m_logger;
        private readonly IConfiguration m_config;

        public UserService(UserManager<IdentityUser> userManager, ILoggerFactory logger, IConfiguration config)
        {
            m_userManager = userManager;
            m_logger = logger.CreateLogger("UserService");
            m_config = config;
        }

        public async Task<bool> ValidateCredentialsAsync(string username, string password)
        {
            if (!String.IsNullOrWhiteSpace(password))
            {
                IdentityUser user = await m_userManager.FindByNameAsync(username).ConfigureAwait(false);
                if (user == null)
                {
                    user = await m_userManager.FindByEmailAsync(username).ConfigureAwait(false);
                }

                if (user != null)
                {
                    PasswordVerificationResult result = m_userManager.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
                    if (result == PasswordVerificationResult.Success)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
