# T1-DH-02 - Redis fail-fast và session lifecycle

## Phạm vi

- Task: `T1-DH-02`
- Mục tiêu: bounded timeout, circuit breaker, fallback cache, revoke semantics, và auth session resilience.
- Không đổi cookie format, auth architecture, hay migration/schema.

## Bằng chứng triển khai

- `src/Qaly.Infrastructure/Auth/RedisTicketStore.cs`
  - Timeout 750ms cho thao tác Redis ticket store.
  - Circuit breaker theo key, mở sau 3 lỗi liên tiếp trong 15 giây.
  - Fallback in-memory cho ticket đã lưu khi Redis lỗi.
- `src/Qaly.Infrastructure/Services/RedisSessionService.cs`
  - Timeout 750ms cho revoke session.
  - Circuit breaker cho revoke flow, trả `false` khi Redis không sẵn sàng.
  - Không báo success giả khi scan/delete thất bại.
- `tests/Qaly.UnitTests/RedisTicketStoreTests.cs`
  - Bổ sung test xác nhận circuit mở rồi hết hạn thì Redis được thử lại.
- `tests/Qaly.IntegrationTests/AuthSessionResilienceTests.cs`
  - Xác minh cookie flags, revoke/logout, và path Redis degraded trong 1 giây.

## Lệnh kiểm tra đã chạy

```powershell
dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj --filter "FullyQualifiedName~RedisTicketStoreTests|FullyQualifiedName~RedisSessionServiceTests" --no-restore
dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj --filter "FullyQualifiedName~AuthSessionResilienceTests" --no-restore
```

## Kết quả

- Unit slice: `14/14` passed.
- Integration slice: `11/11` passed.
- Không có test fail còn lại trong phạm vi T1-DH-02.

## Rollback

- Revert các file liên quan nếu cần quay lại trạng thái trước task:
  - `src/Qaly.Infrastructure/Auth/RedisTicketStore.cs`
  - `src/Qaly.Infrastructure/Services/RedisSessionService.cs`
  - `tests/Qaly.UnitTests/RedisTicketStoreTests.cs`
  - `tests/Qaly.IntegrationTests/AuthSessionResilienceTests.cs`
- Không có migration, data seed, hoặc thay đổi cookie contract cần rollback riêng.
