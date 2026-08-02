<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { Building2, Pencil, Plus, RefreshCw, Search, Users, XCircle } from 'lucide-vue-next'
import PageStatePanel from '../components/PageStatePanel.vue'
import { confirmDialog } from '../composables/use-confirm-dialog'
import { showError, showSuccess } from '../composables/use-toast'
import { apiCommand, apiJson, apiResult, errorMessage } from '../utils/api-client'

interface Organization {
  id: string
  name: string
  code: string
  description?: string | null
  ownerId: string
  ownerName: string
  memberCount: number
  projectCount: number
  isActive: boolean
  allowedEmailDomains?: string | null
  workspaceIcon?: string | null
  workspaceCover?: string | null
}

interface OrganizationPage {
  items: Organization[]
  totalCount: number
}

interface AdminUser {
  id: string
  fullName: string
  email: string
  isActive: boolean
}

interface AdminUserPage {
  items: AdminUser[]
}

const router = useRouter()
const organizations = ref<Organization[]>([])
const owners = ref<AdminUser[]>([])
const isLoading = ref(true)
const isRefreshing = ref(false)
const loadError = ref('')
const saving = ref(false)
const search = ref('')
const status = ref<'active' | 'inactive' | 'all'>('active')
const editorOpen = ref(false)
const editingId = ref<string | null>(null)
const form = ref({ name: '', code: '', description: '', ownerId: '' })

const visibleOrganizations = computed(() => {
  const term = search.value.trim().toLowerCase()

  return organizations.value.filter((item) => {
    const matchesStatus =
      status.value === 'all' ||
      (status.value === 'active' ? item.isActive : !item.isActive)

    const matchesTerm =
      !term ||
      item.name.toLowerCase().includes(term) ||
      item.code.toLowerCase().includes(term) ||
      item.ownerName.toLowerCase().includes(term)

    return matchesStatus && matchesTerm
  })
})

const hasOrganizations = computed(() => organizations.value.length > 0)
const isInitialLoad = computed(() => isLoading.value && !hasOrganizations.value)

async function load({ refreshing = false } = {}) {
  loadError.value = ''
  if (refreshing) {
    isRefreshing.value = true
  } else {
    isLoading.value = true
  }

  try {
    const [organizationPage, userPage] = await Promise.all([
      apiResult<OrganizationPage>('/api/organizations?pageSize=100'),
      apiJson<AdminUserPage>('/api/admin/users?isActive=true&pageSize=100'),
    ])

    organizations.value = organizationPage.items
    owners.value = userPage.items.filter((item) => item.isActive)
  } catch (error) {
    loadError.value = errorMessage(error, 'Không thể tải danh sách tổ chức.')
    showError(loadError.value)
  } finally {
    isLoading.value = false
    isRefreshing.value = false
  }
}

function openCreate() {
  editingId.value = null
  form.value = {
    name: '',
    code: '',
    description: '',
    ownerId: owners.value[0]?.id ?? '',
  }
  editorOpen.value = true
}

function openEdit(item: Organization) {
  editingId.value = item.id
  form.value = {
    name: item.name,
    code: item.code,
    description: item.description ?? '',
    ownerId: item.ownerId,
  }
  editorOpen.value = true
}

async function save() {
  if (!form.value.name.trim() || !form.value.ownerId) return

  saving.value = true
  try {
    const payload = {
      name: form.value.name.trim(),
      code: form.value.code.trim() || null,
      description: form.value.description.trim() || null,
      ownerId: form.value.ownerId,
    }

    if (editingId.value) {
      const current = organizations.value.find((item) => item.id === editingId.value)
      await apiCommand(`/api/organizations/${editingId.value}`, {
        method: 'PUT',
        body: JSON.stringify({
          ...payload,
          isActive: current?.isActive ?? true,
          allowedEmailDomains: current?.allowedEmailDomains ?? null,
          workspaceIcon: current?.workspaceIcon ?? null,
          workspaceCover: current?.workspaceCover ?? null,
        }),
      })
      editorOpen.value = false
      showSuccess('Đã cập nhật tổ chức.')
      await load({ refreshing: true })
      return
    }

    const created = await apiResult<Organization>('/api/organizations', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
    editorOpen.value = false
    showSuccess('Đã tạo tổ chức. Bạn có thể thêm thành viên ngay bây giờ.')
    await router.push({ path: '/organizations/users', query: { organization: created.id } })
  } catch (error) {
    showError(
      errorMessage(
        error,
        editingId.value ? 'Không thể cập nhật tổ chức.' : 'Không thể tạo tổ chức.',
      ),
    )
  } finally {
    saving.value = false
  }
}

async function deactivate(item: Organization) {
  const confirmed = await confirmDialog({
    tone: 'critical',
    title: 'Vô hiệu hóa tổ chức?',
    subject: item.name,
    message:
      'Tổ chức sẽ không còn xuất hiện trong các lựa chọn đang hoạt động. Dữ liệu hiện có không bị xóa.',
    confirmLabel: 'Vô hiệu hóa',
  })

  if (!confirmed) return

  try {
    await apiCommand(`/api/organizations/${item.id}`, { method: 'DELETE' })
    showSuccess('Đã vô hiệu hóa tổ chức.')
    await load({ refreshing: true })
  } catch (error) {
    showError(errorMessage(error, 'Không thể vô hiệu hóa tổ chức.'))
  }
}

function openMembers(item: Organization) {
  router.push({ path: '/organizations/users', query: { organization: item.id } })
}

onMounted(load)
</script>

<template>
  <main class="organizations-page">
    <header class="page-head">
      <div>
        <span class="kicker"><Building2 :size="16" /> Quản trị không gian làm việc</span>
        <h1>Quản lý tổ chức</h1>
        <p>
          Tạo tổ chức, chỉ định chủ sở hữu và quản lý vòng đời của từng không gian làm việc.
        </p>
      </div>
      <button class="primary" :disabled="isInitialLoad || !owners.length" @click="openCreate">
        <Plus :size="18" /> Tạo tổ chức
      </button>
    </header>

    <PageStatePanel
      v-if="isInitialLoad"
      variant="loading"
      title="Đang tải tổ chức"
      message="Hệ thống đang đồng bộ danh sách tổ chức và chủ sở hữu."
    />

    <PageStatePanel
      v-else-if="loadError && !hasOrganizations"
      variant="error"
      title="Không thể tải dữ liệu"
      :message="loadError"
    >
      <template #icon>
        <RefreshCw :size="22" />
      </template>
      <template #actions>
        <button class="primary" type="button" @click="load({ refreshing: true })">
          Thử lại
        </button>
      </template>
    </PageStatePanel>

    <template v-else>
      <section class="toolbar" aria-label="Bộ lọc tổ chức">
        <label class="search">
          <Search :size="18" />
          <input v-model="search" placeholder="Tìm theo tên, mã hoặc chủ sở hữu" />
        </label>
        <select v-model="status" aria-label="Lọc trạng thái">
          <option value="active">Đang hoạt động</option>
          <option value="inactive">Đã vô hiệu hóa</option>
          <option value="all">Mọi trạng thái</option>
        </select>
        <button class="icon-button" :title="isRefreshing ? 'Đang tải lại' : 'Tải lại'" @click="load({ refreshing: true })">
          <RefreshCw :size="18" :class="{ 'is-spinning': isRefreshing }" />
        </button>
      </section>

      <PageStatePanel
        v-if="!visibleOrganizations.length"
        :variant="hasOrganizations ? 'empty' : 'success'"
        :title="hasOrganizations ? 'Không tìm thấy tổ chức' : 'Chưa có tổ chức nào'"
        :message="
          hasOrganizations
            ? 'Thử thay đổi từ khóa hoặc bộ lọc trạng thái.'
            : 'Tạo tổ chức đầu tiên để bắt đầu quản lý thành viên và dự án.'
        "
      >
        <template #icon>
          <Building2 :size="22" />
        </template>
        <template #actions>
          <button v-if="owners.length" class="primary" type="button" @click="openCreate">
            <Plus :size="18" /> Tạo tổ chức
          </button>
        </template>
      </PageStatePanel>

      <div v-else class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Tổ chức</th>
              <th>Chủ sở hữu</th>
              <th>Quy mô</th>
              <th>Trạng thái</th>
              <th><span class="sr-only">Thao tác</span></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in visibleOrganizations" :key="item.id">
              <td>
                <div class="organization">
                  <span class="mark"><Building2 :size="19" /></span>
                  <span>
                    <strong>{{ item.name }}</strong>
                    <small>{{ item.code }}</small>
                  </span>
                </div>
              </td>
              <td>{{ item.ownerName }}</td>
              <td>
                <span class="metric">{{ item.memberCount }} thành viên</span>
                <span class="metric">{{ item.projectCount }} dự án</span>
              </td>
              <td>
                <span class="status" :class="{ inactive: !item.isActive }">
                  {{ item.isActive ? 'Đang hoạt động' : 'Đã vô hiệu hóa' }}
                </span>
              </td>
              <td class="actions">
                <button
                  class="action-button"
                  title="Quản lý thành viên"
                  :disabled="!item.isActive"
                  @click="openMembers(item)"
                >
                  <Users :size="17" />
                </button>
                <button class="action-button" title="Chỉnh sửa" @click="openEdit(item)">
                  <Pencil :size="17" />
                </button>
                <button
                  v-if="item.isActive"
                  class="action-button danger"
                  title="Vô hiệu hóa"
                  @click="deactivate(item)"
                >
                  <XCircle :size="17" />
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>

    <div v-if="editorOpen" class="overlay" @click.self="editorOpen = false">
      <form class="modal" @submit.prevent="save">
        <div>
          <h2>{{ editingId ? 'Chỉnh sửa tổ chức' : 'Tạo tổ chức' }}</h2>
          <p>Chủ sở hữu sẽ nhận toàn quyền quản lý trong tổ chức này.</p>
        </div>
        <label>
          Tên tổ chức
          <input v-model.trim="form.name" required maxlength="160" placeholder="Ví dụ: Qaly Product" />
        </label>
        <label>
          Mã tổ chức
          <input v-model.trim="form.code" maxlength="80" placeholder="Tự tạo nếu để trống" />
          <small>Dùng chữ cái, số và dấu gạch ngang.</small>
        </label>
        <label>
          Chủ sở hữu
          <select v-model="form.ownerId" required>
            <option v-for="owner in owners" :key="owner.id" :value="owner.id">
              {{ owner.fullName }} - {{ owner.email }}
            </option>
          </select>
        </label>
        <label>
          Mô tả
          <textarea
            v-model.trim="form.description"
            rows="3"
            maxlength="500"
            placeholder="Mục đích và phạm vi của tổ chức"
          />
        </label>
        <div class="modal-actions">
          <button type="button" class="secondary" @click="editorOpen = false">Hủy</button>
          <button class="primary" :disabled="saving || !form.name || !form.ownerId">
            {{ saving ? 'Đang lưu...' : editingId ? 'Lưu thay đổi' : 'Tạo và thêm thành viên' }}
          </button>
        </div>
      </form>
    </div>
  </main>
</template>

<style scoped>
.organizations-page {
  max-width: 1320px;
  margin: auto;
  padding: 32px;
  color: var(--text-primary, #162033);
  display: grid;
  gap: 16px;
}

.page-head {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 24px;
}

.page-head h1 {
  font-size: 30px;
  margin: 7px 0;
}

.page-head p {
  max-width: 720px;
  margin: 0;
  color: var(--text-secondary, #687386);
}

.kicker {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  color: #166534;
  font-size: 13px;
  font-weight: 750;
}

.primary,
.secondary,
.icon-button,
.action-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border: 1px solid transparent;
  border-radius: 10px;
  padding: 10px 15px;
  font-weight: 700;
  white-space: nowrap;
  cursor: pointer;
}

.primary {
  background: #166534;
  color: #fff;
}

.secondary,
.icon-button,
.action-button {
  background: var(--surface, #fff);
  border-color: var(--border-color, #dce2ea);
  color: inherit;
}

.primary:active,
.secondary:active,
.icon-button:active,
.action-button:active {
  transform: translateY(1px);
}

button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.toolbar {
  display: grid;
  grid-template-columns: minmax(280px, 1fr) 190px 44px;
  gap: 10px;
}

.search {
  display: flex;
  align-items: center;
  gap: 9px;
  height: 44px;
  padding: 0 12px;
  border: 1px solid var(--border-color, #dce2ea);
  border-radius: 10px;
  background: var(--surface, #fff);
}

.search input {
  width: 100%;
  border: 0;
  outline: 0;
  background: transparent;
  color: inherit;
}

.toolbar select,
.modal input,
.modal select,
.modal textarea {
  border: 1px solid var(--border-color, #dce2ea);
  border-radius: 9px;
  background: var(--surface, #fff);
  color: inherit;
}

.toolbar select {
  height: 44px;
  padding: 0 11px;
}

.icon-button {
  padding: 0;
}

.table-wrap {
  overflow: auto;
  border: 1px solid var(--border-color, #dce2ea);
  border-radius: 14px;
  background: var(--surface, #fff);
}

table {
  width: 100%;
  min-width: 900px;
  border-collapse: collapse;
}

th,
td {
  text-align: left;
  padding: 14px 16px;
  border-bottom: 1px solid var(--border-color, #e8edf3);
}

th {
  font-size: 12px;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--text-secondary, #687386);
  background: var(--surface-muted, #f7f9fb);
}

.organization {
  display: flex;
  align-items: center;
  gap: 11px;
}

.organization > span:last-child {
  display: grid;
  gap: 3px;
}

.organization small {
  color: var(--text-secondary, #687386);
}

.mark {
  width: 38px;
  height: 38px;
  border-radius: 10px;
  display: grid;
  place-items: center;
  background: #dcfce7;
  color: #166534;
}

.metric {
  display: block;
  color: var(--text-secondary, #687386);
  font-size: 13px;
  line-height: 1.6;
}

.status {
  display: inline-flex;
  padding: 5px 9px;
  border-radius: 999px;
  background: #dcfce7;
  color: #166534;
  font-size: 12px;
  font-weight: 700;
}

.status.inactive {
  background: var(--surface-muted, #f1f5f9);
  color: var(--text-secondary, #687386);
}

.actions {
  text-align: right;
  white-space: nowrap;
}

.action-button {
  padding: 8px;
  margin-left: 6px;
}

.action-button.danger {
  color: #b91c1c;
  border-color: #fecaca;
}

.overlay {
  position: fixed;
  inset: 0;
  z-index: 1000;
  display: grid;
  place-items: center;
  padding: 16px;
  background: rgba(15, 23, 42, 0.48);
}

.modal {
  width: min(520px, 100%);
  display: grid;
  gap: 16px;
  padding: 26px;
  border-radius: 14px;
  background: var(--surface, #fff);
  box-shadow: 0 24px 70px rgba(15, 23, 42, 0.22);
}

.modal h2,
.modal p {
  margin: 0;
}

.modal p {
  margin-top: 6px;
  color: var(--text-secondary, #687386);
}

.modal label {
  display: grid;
  gap: 7px;
  font-size: 13px;
  font-weight: 700;
}

.modal input,
.modal select {
  height: 42px;
  padding: 0 11px;
}

.modal textarea {
  padding: 10px 11px;
  resize: vertical;
}

.modal label small {
  color: var(--text-secondary, #687386);
  font-weight: 500;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 4px;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

.is-spinning {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 720px) {
  .organizations-page {
    padding: 20px 16px;
  }

  .page-head {
    align-items: stretch;
    flex-direction: column;
  }

  .toolbar {
    grid-template-columns: 1fr 44px;
  }

  .toolbar select {
    grid-column: 1 / -1;
    grid-row: 2;
  }

  .modal-actions {
    flex-direction: column-reverse;
  }

  .modal-actions button {
    width: 100%;
  }
}

@media (prefers-reduced-motion: reduce) {
  .is-spinning {
    animation: none;
  }
}
</style>
