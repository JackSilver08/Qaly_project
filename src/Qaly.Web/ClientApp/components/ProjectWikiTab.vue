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
  deleteWikiPage,
  updateWikiPage,
  formatDate
} = useDashboardContext()

const showAddForm = ref(false)
const newPageTitle = ref('')

async function handleCreate() {
  if (!newPageTitle.value.trim()) return
  await createWikiPage(newPageTitle.value.trim())
  newPageTitle.value = ''
  showAddForm.value = false
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
        <span>Knowledge Base</span>
        <h2>{{ projectName }} Wiki</h2>
      </div>
      <button v-if="isAdmin" class="primary-button primary-button--compact" type="button" @click="showAddForm = !showAddForm">
        <Plus :size="16" />
        <span>Tạo trang mới</span>
      </button>
    </div>

    <!-- Add Wiki Form -->
    <div v-if="showAddForm" class="wiki-add-form glass-card reveal">
      <h3>Tạo trang Wiki mới</h3>
      <div class="form-row">
        <input v-model="newPageTitle" type="text" placeholder="Tiêu đề trang..." @keyup.enter="handleCreate" />
        <button class="primary-button" type="button" :disabled="!newPageTitle.trim()" @click="handleCreate">Tạo</button>
        <button class="text-button" type="button" @click="showAddForm = false">Hủy</button>
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
          <button class="icon-button icon-button--small" type="button" title="Sửa">
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
      <button v-if="isAdmin" class="secondary-button" type="button" style="margin-top: 16px;" @click="showAddForm = true">Bắt đầu viết Wiki</button>
    </div>
  </div>
</template>

<style scoped>
.wiki-tab-content {
  padding: 24px;
  background: white;
  border: 1px solid var(--glass-border);
}

.wiki-add-form {
  padding: 20px;
  margin-bottom: 24px;
  background: var(--surface-warm);
  border: 1px solid var(--line);
  border-radius: 12px;
}

.wiki-add-form h3 {
  font-size: 15px;
  margin-bottom: 12px;
  color: var(--text);
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
  border: 1px solid var(--line);
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
  background: var(--surface-milk);
  border: 1px solid var(--line);
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
}

.wiki-item {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 14px 18px;
  background: white;
  border: 1px solid var(--line);
  border-radius: 14px;
  transition: all 0.2s;
}

.wiki-item:hover {
  border-color: var(--primary);
  box-shadow: var(--shadow-card);
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
  color: var(--text);
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
  background: var(--primary-soft);
  color: var(--primary);
  display: grid;
  place-items: center;
  margin-bottom: 8px;
}

.wiki-empty h3 {
  font-size: 18px;
  margin: 0;
  color: var(--text);
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
  color: var(--text);
}

.panel-heading span {
  color: var(--muted);
  font-size: 13px;
  font-weight: 600;
  text-transform: uppercase;
}

.icon-button.risk:hover {
  color: var(--peach-500);
  background: var(--peach-100);
}
</style>
