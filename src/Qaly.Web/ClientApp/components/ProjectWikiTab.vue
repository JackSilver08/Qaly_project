<script setup lang="ts">
import { ref } from 'vue'
import { FileText, Plus, Pencil, Trash2, Search } from 'lucide-vue-next'
import { useDashboardContext } from '../composables/dashboard-context'

const props = defineProps<{
  projectName: string
  isAdmin: boolean
}>()

const {
  wikiPages,
  createWikiPage,
  updateWikiPage,
  deleteWikiPage,
  formatDate,
} = useDashboardContext()

const showAddForm = ref(false)
const newPageTitle = ref('')
const editingPageId = ref<string | null>(null)

async function handleCreate() {
  const title = newPageTitle.value.trim()
  if (!title) return

  const saved = editingPageId.value
    ? await updateWikiPage(editingPageId.value, title, '')
    : await createWikiPage(title)

  if (saved) {
    newPageTitle.value = ''
    editingPageId.value = null
    showAddForm.value = false
  }
}

function startEdit(page: { id: string; title: string }) {
  editingPageId.value = page.id
  newPageTitle.value = page.title
  showAddForm.value = true
}

function cancelEditor() {
  showAddForm.value = false
  editingPageId.value = null
  newPageTitle.value = ''
}

function toggleAddForm() {
  if (showAddForm.value) {
    cancelEditor()
    return
  }

  showAddForm.value = true
}

async function handleDelete(id: string) {
  if (!confirm('Bạn có chắc chắn muốn xóa trang Wiki này?')) return
  await deleteWikiPage(id)
}
</script>

<template>
  <div class="wiki-tab-content glass-card">
    <div class="panel-heading">
      <div>
        <span class="panel-heading__eyebrow">Knowledge Base</span>
        <h2>{{ projectName }} Wiki</h2>
      </div>
      <button class="primary-button primary-button--compact" type="button" @click="toggleAddForm">
        <Plus :size="16" />
        <span>Tạo trang mới</span>
      </button>
    </div>

    <div v-if="showAddForm" class="wiki-add-form glass-card reveal">
      <h3>{{ editingPageId ? 'Sửa trang Wiki' : 'Tạo trang Wiki mới' }}</h3>
      <div class="form-row">
        <input
          v-model="newPageTitle"
          type="text"
          placeholder="Tiêu đề trang..."
          @keyup.enter="handleCreate"
        />
        <button class="primary-button" type="button" :disabled="!newPageTitle.trim()" @click="handleCreate">
          {{ editingPageId ? 'Cập nhật' : 'Tạo' }}
        </button>
        <button class="text-button" type="button" @click="cancelEditor">Hủy</button>
      </div>
    </div>

    <div v-if="wikiPages.length > 0" class="wiki-list">
      <div class="wiki-search-bar">
        <Search :size="16" />
        <input type="text" placeholder="Tìm kiếm trang Wiki..." />
      </div>

      <article v-for="page in wikiPages" :key="page.id" class="wiki-item">
        <div class="wiki-item__icon">
          <FileText :size="20" />
        </div>
        <div class="wiki-item__main">
          <strong>{{ page.title }}</strong>
          <span>Cập nhật bởi {{ page.authorName }} vào {{ formatDate(page.updatedAt) }}</span>
        </div>
        <div v-if="isAdmin" class="wiki-item__actions">
          <button class="icon-button icon-button--small" type="button" title="Sửa" @click="startEdit(page)">
            <Pencil :size="14" />
          </button>
          <button class="icon-button icon-button--small risk" type="button" title="Xóa" @click="handleDelete(page.id)">
            <Trash2 :size="14" />
          </button>
        </div>
      </article>
    </div>

    <div v-else class="wiki-empty">
      <div class="wiki-empty__icon">
        <FileText :size="48" />
      </div>
      <h3>Chưa có trang Wiki nào</h3>
      <p>Wiki là nơi lưu trữ kiến thức dự án, guideline và quy trình làm việc của team.</p>
      <button class="secondary-button" type="button" style="margin-top: 16px;" @click="showAddForm = true">Bắt đầu viết Wiki</button>
    </div>
  </div>
</template>

<style scoped>
.wiki-tab-content {
  padding: 24px;
  background: rgba(8, 21, 39, 0.62);
  border: 1px solid var(--glass-border);
}

.wiki-tab-content :deep(.primary-button),
.wiki-tab-content :deep(.primary-button span) {
  color: #fff;
}

.wiki-add-form {
  padding: 20px;
  margin-bottom: 24px;
  background: rgba(31, 128, 255, 0.12);
  border: 1px solid rgba(117, 182, 255, 0.3);
  border-radius: 12px;
}

.wiki-add-form h3 {
  font-size: 15px;
  margin-bottom: 12px;
  color: var(--surface-milk);
  font-weight: 700;
}

.form-row {
  display: flex;
  gap: 12px;
}

.form-row input {
  flex: 1;
  padding: 8px 12px;
  border-radius: 8px;
  border: 1px solid rgba(182, 194, 217, 0.24);
  color: var(--surface-milk);
  background: rgba(8, 21, 39, 0.74);
}

.wiki-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.wiki-search-bar {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 16px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(182, 194, 217, 0.24);
  border-radius: 12px;
  margin-bottom: 8px;
  color: var(--muted);
}

.wiki-search-bar input {
  border: 0;
  background: transparent;
  flex: 1;
  font-size: 14px;
  outline: none;
  color: var(--surface-milk);
}

.wiki-item {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 14px 18px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(182, 194, 217, 0.24);
  border-radius: 14px;
  transition: all 0.2s;
}

.wiki-item:hover {
  border-color: rgba(117, 182, 255, 0.62);
  box-shadow: 0 16px 32px rgba(15, 76, 255, 0.18);
}

.wiki-item__icon {
  color: var(--primary);
}

.wiki-item__main {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.wiki-item__main strong {
  font-size: 15px;
  color: var(--surface-milk);
}

.wiki-item__main span {
  font-size: 12px;
  color: var(--muted);
}

.wiki-item__actions {
  display: flex;
  gap: 8px;
}

.wiki-empty {
  padding: 60px 20px;
  text-align: center;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
}

.wiki-empty__icon {
  width: 80px;
  height: 80px;
  border-radius: 20px;
  background: linear-gradient(135deg, #0f4cff, #22d3ee);
  color: #f8fafc;
  display: grid;
  place-items: center;
  margin-bottom: 8px;
}

.wiki-empty h3 {
  font-size: 18px;
  margin: 0;
  color: var(--surface-milk);
  font-weight: 700;
}

.wiki-empty p {
  color: var(--muted);
  max-width: 400px;
  margin: 0;
  font-size: 14px;
}

.panel-heading {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.panel-heading h2 {
  font-size: 18px;
  font-weight: 800;
  color: var(--surface-milk);
}

.panel-heading__eyebrow {
  color: var(--muted);
  font-size: 13px;
  font-weight: 600;
  text-transform: uppercase;
}

.icon-button.risk:hover {
  color: #fecaca;
  background: rgba(239, 68, 68, 0.2);
}
</style>
