using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Qaly.Application.DTOs.User;
using Qaly.Application.Services;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.UnitTests;

public class AuthServiceTests
{
    [Fact]
    public async Task RegisterThenLoginReturnsCreatedUser()
    {
        var users = new InMemoryRepository<User>();
        var service = CreateService(users);

        var register = await service.RegisterAsync(new RegisterDto(
            "Test User",
            "test@qaly.dev",
            "Password@123",
            "Password@123"));

        var login = await service.LoginAsync(new LoginDto("test@qaly.dev", "Password@123"));

        register.IsSuccess.Should().BeTrue();
        login.IsSuccess.Should().BeTrue();
        login.Data!.Email.Should().Be("test@qaly.dev");
    }

    [Fact]
    public async Task LoginRejectsWrongPassword()
    {
        var users = new InMemoryRepository<User>();
        var service = CreateService(users);

        await service.RegisterAsync(new RegisterDto(
            "Test User",
            "test@qaly.dev",
            "Password@123",
            "Password@123"));

        var login = await service.LoginAsync(new LoginDto("test@qaly.dev", "not-the-password"));

        login.IsSuccess.Should().BeFalse();
        login.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task LoginRejectsInactiveAccountWithTheSameSafeMessage()
    {
        var users = new InMemoryRepository<User>();
        var service = CreateService(users);

        var registration = await service.RegisterAsync(new RegisterDto(
            "Inactive User",
            "inactive@qaly.dev",
            "Password@123",
            "Password@123"));
        var user = await users.GetByIdAsync(registration.Data!.Id);
        user!.IsActive = false;

        var login = await service.LoginAsync(new LoginDto("inactive@qaly.dev", "Password@123"));

        login.IsSuccess.Should().BeFalse();
        login.StatusCode.Should().Be(401);
        login.Error.Should().Be("Email hoặc mật khẩu không đúng.");
    }

    [Fact]
    public async Task LoginSynchronizesAnExistingAccountWithTheConfiguredSeedPassword()
    {
        var users = new InMemoryRepository<User>();
        var seedCredentials = new Mock<ISeedCredentialProvider>();
        seedCredentials
            .Setup(item => item.GetPassword("admin@qaly.dev"))
            .Returns("ConfiguredSeed@123");
        var service = CreateService(users, seedCredentials.Object);

        await service.RegisterAsync(new RegisterDto(
            "Demo Admin",
            "admin@qaly.dev",
            "PreviousSeed@123",
            "PreviousSeed@123"));

        var firstLogin = await service.LoginAsync(new LoginDto("admin@qaly.dev", "ConfiguredSeed@123"));
        var secondLogin = await service.LoginAsync(new LoginDto("admin@qaly.dev", "ConfiguredSeed@123"));

        firstLogin.IsSuccess.Should().BeTrue();
        secondLogin.IsSuccess.Should().BeTrue("the synchronized hash must be persisted after the first login");
    }

    private static AuthService CreateService(
        IRepository<User> users,
        ISeedCredentialProvider? seedCredentialProvider = null)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var audit = new Mock<IAuditLogService>();
        audit.Setup(item => item.LogAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sessionService = new Mock<ISessionService>();
        seedCredentialProvider ??= Mock.Of<ISeedCredentialProvider>();
        
        return new AuthService(
            users,
            unitOfWork.Object,
            audit.Object,
            sessionService.Object,
            seedCredentialProvider);
    }

    private sealed class InMemoryRepository<T> : IRepository<T> where T : BaseEntity
    {
        private readonly List<T> _items = [];

        public IQueryable<T> GetQueryable()
            => _items.AsQueryable();

        public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(item => item.Id == id));

        public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<T>>(_items.ToList());

        public Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<T>>(_items.AsQueryable().Where(predicate).ToList());

        public Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            _items.Add(entity);
            return Task.FromResult(entity);
        }

        public Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            _items.AddRange(entities);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            _items.Remove(entity);
            return Task.CompletedTask;
        }

        public Task HardDeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            _items.Remove(entity);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.Any(item => item.Id == id));

        public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
            => Task.FromResult(predicate == null ? _items.Count : _items.AsQueryable().Count(predicate));
    }
}
