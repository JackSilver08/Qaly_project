<script setup lang="ts">
/**
 * Lets an organization owner or admin define roles that match how their team actually works
 * ("Dev Backend", "PO", "AI Engineer") on top of the built-in ones.
 *
 * A custom role is a label plus an inherited permission level: it can never grant more than the
 * built-in role it is based on.
 */
import { computed, ref, watch } from 'vue'
import { Pencil, Plus, Sparkles, Trash2, X } from 'lucide-vue-next'
import { apiCommand, apiResult, errorMessage } from '../utils/api-client'
import { showError, showSuccess } from '../composables/use-toast'
import { PROJECT_ROLE_OPTIONS } from '../utils/project-roles'

interface RoleDefinition {
  id: string
  organizationId: string
  key: string
  displayName: string
  description: string | null
  baseRole: string
  baseRoleLabel: string
  permissionSummary: string
  aiTier: string
  aiTierDescription: string
  skillTags: string[]
  isActive: boolean
  memberCount: number
}

const props = defineProps<{
  organizationId: string | null
  canManage: boolean
}>()

const emit = defineEmits<{ changed: [] }>()

const definitions = ref<RoleDefinition[]>([])
const loading = ref(false)
const saving = ref(false)
const formOpen = ref(false)
const editingId = ref<string | null>(null)
const serverCanManage = ref(false)
// The organization-scoped API is authoritative. Project-level manager permission belongs to a
// different RBAC boundary and must not grant or suppress custom-role administration.
const effectiveCanManage = computed(() => serverCanManage.value)

const displayName = ref('')
const baseRole = ref('Member')
const description = ref('')
const skillTags = ref('')

const baseRoleOptions = PROJECT_ROLE_OPTIONS

const previewKey = computed(() =>
  displayName.value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, ''),
)

async function load() {
  if (!props.organizationId) {
    definitions.value = []
    serverCanManage.value = false
    return
  }
  loading.value = true
  try {
    const [roles, canManage] = await Promise.all([
      apiResult<RoleDefinition[]>(`/api/organizations/${props.organizationId}/role-definitions`),
      apiResult<boolean>(`/api/organizations/${props.organizationId}/role-definitions/access`),
    ])
    definitions.value = roles
    serverCanManage.value = canManage
  } catch (e) {
    showError(errorMessage(e, 'Không tải được danh sách vai trò.'))
  } finally {
    loading.value = false
  }
}

watch(() => props.organizationId, load, { immediate: true })

function resetForm() {
  displayName.value = ''
  baseRole.value = 'Member'
  description.value = ''
  skillTags.value = ''
  formOpen.value = false
  editingId.value = null
}

function edit(definition: RoleDefinition) {
  editingId.value = definition.id
  displayName.value = definition.displayName
  baseRole.value = definition.baseRole
  description.value = definition.description ?? ''
  skillTags.value = definition.skillTags.join(', ')
  formOpen.value = true
}

async function save() {
  if (!props.organizationId || !displayName.value.trim()) return
  saving.value = true
  try {
    const isEditing = editingId.value !== null
    const current = definitions.value.find(item => item.id === editingId.value)
    await apiCommand(isEditing
      ? `/api/role-definitions/${editingId.value}`
      : `/api/organizations/${props.organizationId}/role-definitions`, {
      method: isEditing ? 'PUT' : 'POST',
      body: JSON.stringify({
        displayName: displayName.value.trim(),
        baseRole: baseRole.value,
        description: description.value.trim() || null,
        skillTags: skillTags.value.trim() || null,
        ...(isEditing ? { isActive: current?.isActive ?? true } : {}),
      }),
    })
    showSuccess(isEditing
      ? `Đã cập nhật vai trò "${displayName.value.trim()}".`
      : `Đã tạo vai trò "${displayName.value.trim()}".`)
    resetForm()
    await load()
    emit('changed')
  } catch (e) {
    showError(errorMessage(e, 'Không tạo được vai trò.'))
  } finally {
    saving.value = false
  }
}

async function toggleActive(definition: RoleDefinition) {
  saving.value = true
  try {
    await apiCommand(`/api/role-definitions/${definition.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        displayName: definition.displayName,
        baseRole: definition.baseRole,
        description: definition.description,
        skillTags: definition.skillTags.join(','),
        isActive: !definition.isActive,
      }),
    })
    await load()
    emit('changed')
  } catch (e) {
    showError(errorMessage(e, 'Không cập nhật được vai trò.'))
  } finally {
    saving.value = false
  }
}

async function remove(definition: RoleDefinition) {
  saving.value = true
  try {
    await apiCommand(`/api/role-definitions/${definition.id}`, { method: 'DELETE' })
    showSuccess(`Đã xóa vai trò "${definition.displayName}".`)
    await load()
    emit('changed')
  } catch (e) {
    showError(errorMessage(e, 'Không xóa được vai trò.'))
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <section v-if="organizationId" class="role-manager">
    <header>
      <div>
        <span class="eyebrow">Vai trò riêng của tổ chức</span>
        <p class="lede">
          Tạo vai trò theo cách nhóm bạn làm việc. Mỗi vai trò kế thừa quyền của một vai trò có sẵn.
        </p>
      </div>
      <button v-if="effectiveCanManage" class="primary-button primary-button--compact" type="button" @click="formOpen = !formOpen">
        <component :is="formOpen ? X : Plus" :size="15" />
        <span>{{ formOpen ? 'Đóng' : 'Tạo vai trò' }}</span>
      </button>
    </header>

    <form v-if="formOpen && effectiveCanManage" class="role-form" @submit.prevent="save">
      <div class="field">
        <label for="role-name">Tên vai trò</label>
        <input id="role-name" v-model="displayName" placeholder="VD: Lập trình viên Backend" maxlength="80" />
        <small v-if="previewKey">Mã lưu trữ: <code>{{ previewKey }}</code></small>
      </div>

      <div class="field">
        <label for="role-base">Kế thừa quyền từ</label>
        <select id="role-base" v-model="baseRole">
          <option v-for="option in baseRoleOptions" :key="option.value" :value="option.value">
            {{ option.label }}
          </option>
        </select>
        <small>{{ baseRoleOptions.find(o => o.value === baseRole)?.hint }}</small>
      </div>

      <div class="field">
        <label for="role-skills">Kỹ năng (phân tách bằng dấu phẩy)</label>
        <input id="role-skills" v-model="skillTags" placeholder="backend, dotnet, sql" maxlength="400" />
        <small>Dùng để gợi ý người phù hợp khi phân task.</small>
      </div>

      <div class="field field--wide">
        <label for="role-desc">Mô tả</label>
        <input id="role-desc" v-model="description" placeholder="Vai trò này phụ trách những gì?" maxlength="400" />
      </div>

      <div class="form-actions">
        <button class="primary-button" type="submit" :disabled="saving || !displayName.trim()">
          {{ editingId ? 'Lưu thay đổi' : 'Tạo vai trò' }}
        </button>
        <button class="text-button" type="button" @click="resetForm">Hủy</button>
      </div>
    </form>

    <p v-if="loading" class="muted">Đang tải vai trò...</p>
    <p v-else-if="!definitions.length" class="muted">
      Chưa có vai trò riêng. Tổ chức đang dùng {{ baseRoleOptions.length }} vai trò mặc định.
    </p>

    <ul v-else class="definition-list">
      <li v-for="definition in definitions" :key="definition.id" :class="{ inactive: !definition.isActive }">
        <div class="definition-main">
          <strong>{{ definition.displayName }}</strong>
          <span class="inherit-chip"><Sparkles :size="12" /> kế thừa {{ definition.baseRoleLabel }}</span>
          <span v-if="!definition.isActive" class="state-chip">Đã tắt</span>
        </div>
        <p v-if="definition.description" class="definition-desc">{{ definition.description }}</p>
        <div class="permission-preview">
          <strong>{{ definition.permissionSummary }}</strong>
          <span>AI {{ definition.aiTier }} · {{ definition.aiTierDescription }}</span>
        </div>
        <div class="definition-meta">
          <span v-for="tag in definition.skillTags" :key="tag" class="skill-chip">{{ tag }}</span>
          <span class="usage">{{ definition.memberCount }} thành viên đang dùng</span>
        </div>
        <div v-if="effectiveCanManage" class="definition-actions">
          <button class="text-button" type="button" :disabled="saving" @click="edit(definition)">
            <Pencil :size="13" /> Sửa
          </button>
          <button class="text-button" type="button" :disabled="saving" @click="toggleActive(definition)">
            {{ definition.isActive ? 'Tắt' : 'Bật lại' }}
          </button>
          <button
            class="icon-button danger"
            type="button"
            :disabled="saving || definition.memberCount > 0"
            :title="definition.memberCount > 0 ? 'Còn thành viên đang dùng vai trò này' : 'Xóa vai trò'"
            @click="remove(definition)"
          >
            <Trash2 :size="15" />
          </button>
        </div>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.role-manager {
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  background: var(--bg-soft);
  padding: 18px;
  display: grid;
  gap: 14px;
  margin-top: 20px;
}

.role-manager > header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}

.eyebrow {
  display: block;
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--muted);
}

.lede {
  margin-top: 4px;
  font-size: 13px;
  color: var(--muted);
  max-width: 62ch;
}

.role-form {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 14px;
  padding: 16px;
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  background: var(--panel);
}

.field {
  display: grid;
  gap: 5px;
}

.field--wide {
  grid-column: 1 / -1;
}

.field label {
  font-size: 12px;
  font-weight: 700;
  color: var(--text-strong);
}

.field input,
.field select {
  padding: 8px 10px;
  border-radius: var(--qaly-radius-lg);
  border: 1px solid var(--line);
  background: var(--panel);
  color: var(--text-strong);
  font-size: 13px;
}

.field small {
  font-size: 11.5px;
  color: var(--muted);
}

.field code {
  background: var(--bg-soft);
  padding: 1px 5px;
  border-radius: 4px;
}

.form-actions {
  grid-column: 1 / -1;
  display: flex;
  gap: 10px;
  align-items: center;
}

.definition-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: 10px;
}

.definition-list li {
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  background: var(--panel);
  padding: 12px 14px;
  display: grid;
  gap: 6px;
  position: relative;
}

.definition-list li.inactive {
  opacity: 0.6;
}

.definition-main {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.definition-main strong {
  font-size: 14px;
  color: var(--text-strong);
}

.inherit-chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 11px;
  font-weight: 700;
  padding: 3px 8px;
  border-radius: 999px;
  background: rgba(31, 128, 255, 0.14);
  color: var(--text-strong);
}

.state-chip {
  font-size: 11px;
  font-weight: 700;
  padding: 3px 8px;
  border-radius: 999px;
  background: var(--bg-soft);
  color: var(--muted);
}

.definition-desc {
  font-size: 12.5px;
  color: var(--muted);
}

.definition-meta {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}

.permission-preview {
  display: grid;
  gap: 2px;
  padding: 8px 10px;
  border-radius: var(--qaly-radius-lg);
  background: var(--bg-soft);
  font-size: 11.5px;
  color: var(--muted);
}

.permission-preview strong {
  color: var(--text-strong);
}

.skill-chip {
  font-size: 11px;
  padding: 2px 7px;
  border-radius: 5px;
  background: var(--bg-soft);
  color: var(--text-strong);
  border: 1px solid var(--line);
}

.usage {
  font-size: 11.5px;
  color: var(--muted);
  margin-left: auto;
}

.definition-actions {
  display: flex;
  gap: 8px;
  align-items: center;
  justify-content: flex-end;
}

.muted {
  font-size: 13px;
  color: var(--muted);
}
</style>
