# ========================================
# Qaly Project - Pull Request Template
# ========================================

## 📋 Mô tả thay đổi
<!-- Mô tả ngắn gọn những gì PR này làm -->


## 🔗 Liên kết
- Issue: #
- Task: 
- RC priority: P0 / P1 / P2 / N/A
- Risk: low / medium / high

## 📝 Loại thay đổi
- [ ] 🐛 Bug fix
- [ ] ✨ Feature mới
- [ ] 🔨 Refactor
- [ ] 📖 Documentation
- [ ] 🧪 Tests
- [ ] 🔧 DevOps / Config

## ✅ Checklist
- [ ] Code đã build thành công (`dotnet build`)
- [ ] Unit tests pass (`dotnet test`)
- [ ] Frontend typecheck/build pass (`npm run typecheck`, `npm run build`)
- [ ] Config safety/parity pass (`./scripts/check-configuration.ps1`, `./scripts/check-config-parity.ps1`)
- [ ] Docker Compose hợp lệ (`docker compose config --quiet`)
- [ ] Đã tự review code
- [ ] Đã cập nhật documentation (nếu cần)
- [ ] Không có hardcode secrets/passwords
- [ ] Migration có ghi chú recovery/restore evidence nếu có rủi ro dữ liệu
- [ ] Release note hoặc demo note đã cập nhật nếu thay đổi ảnh hưởng người dùng

## 🔐 Authorization, tenant và dữ liệu
<!-- Bắt buộc với thay đổi high-risk; ghi N/A và lý do nếu không áp dụng. -->
- [ ] Đã đối chiếu `docs/13_RBAC_Tenant_Authorization_Matrix.md`
- [ ] Có cả test cho phép và test từ chối
- [ ] Đã kiểm tra không đọc/ghi chéo organization/project
- [ ] Background job/export/AI/realtime giữ nguyên tenant context
- [ ] Migration có preflight/postflight/reconciliation và đường phục hồi

## 🧾 Evidence
- Commit đã kiểm tra:
- Unit/integration/E2E:
- Coverage:
- Config/security/container:
- Migration/recovery:

## 📸 Screenshots (nếu có UI changes)
<!-- Thêm screenshots ở đây -->

## 💡 Ghi chú cho Reviewer
<!-- Những điều reviewer cần lưu ý -->
- Ranh giới bảo mật hoặc dữ liệu bị ảnh hưởng:
- Failure/rollback path:
