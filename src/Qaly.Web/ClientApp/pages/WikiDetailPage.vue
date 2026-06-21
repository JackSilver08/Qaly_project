<script setup lang="ts">
import { computed, ref, type Ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useDashboardContext } from '../composables/dashboard-context';
import { ChevronLeft, Pencil, Check, X } from 'lucide-vue-next';
import { MdEditor, MdPreview, MdCatalog } from 'md-editor-v3';
import 'md-editor-v3/lib/style.css';
import 'md-editor-v3/lib/preview.css';
import type { WikiPageDto } from '../types';

const route = useRoute();
const router = useRouter();

const { wikiPages, updateWikiPage, formatDate } = useDashboardContext() as {
  wikiPages: Ref<WikiPageDto[]>;
  updateWikiPage: (
    wikiId: string,
    title: string,
    content: string,
    visibility: string,
  ) => Promise<boolean>;
  formatDate: (value: string) => string;
};

const wikiId = computed(() => route.params.wikiId as string);
const projectId = computed(() => route.params.projectId as string);

const currentPage = computed(() => {
  return wikiPages.value.find((p: WikiPageDto) => p.id === wikiId.value);
});
const showWikiSidebar = computed(() => !isEditing.value && !!currentPage.value?.content);

const isEditing = ref(false);
const editTitle = ref("");
const editContent = ref("");
const editVisibility = ref("internal");

// For ToC scrolling
const scrollElement = document.documentElement;

function goBack() {
  router.push(`/projects/${projectId.value}?tab=wiki`);
}

function startEdit() {
  if (!currentPage.value) return;
  editTitle.value = currentPage.value.title;
  editContent.value = currentPage.value.content || "";
  editVisibility.value = (currentPage.value as any).visibility || "internal";
  isEditing.value = true;
}

function cancelEdit() {
  isEditing.value = false;
}

async function saveEdit() {
  if (!editTitle.value.trim() || !currentPage.value) return;
  const success = await updateWikiPage(
    currentPage.value.id,
    editTitle.value,
    editContent.value,
    editVisibility.value
  );
  if (success) {
    isEditing.value = false;
  }
}
</script>

<template>
  <div class="wiki-detail-page">
    <div class="wiki-detail-container" :class="{ 'editing-mode': isEditing }">
      <div class="wiki-top-nav">
        <button class="back-btn" @click="goBack">
          <ChevronLeft :size="18" /> Quay lại danh sách
        </button>
      </div>

      <div v-if="!currentPage" class="wiki-not-found">
        <h2>Không tìm thấy trang Wiki</h2>
        <p>Trang Wiki này không tồn tại hoặc đã bị xóa.</p>
        <button class="primary-button" @click="goBack">Quay lại</button>
      </div>

      <div v-else class="wiki-layout">
        <!-- Main Document Area -->
        <div class="wiki-document glass-card">
          <div v-if="!isEditing" class="wiki-view-mode">
            <div class="wiki-header">
              <h1 class="wiki-title">{{ currentPage.title }}</h1>
              <div class="wiki-meta">
                <span class="wiki-author">Cập nhật bởi {{ currentPage.authorName }} vào {{ formatDate(currentPage.updatedAt) }}</span>
                <span class="wiki-badge" :class="`badge-${(currentPage as any).visibility || 'internal'}`">
                  {{ (currentPage as any).visibility === 'public' ? 'Công khai' : ((currentPage as any).visibility === 'customer_safe' ? 'Cho khách hàng' : 'Nội bộ') }}
                </span>
              </div>
              <button class="primary-button edit-btn" @click="startEdit">
                <Pencil :size="16" /> Sửa trang
              </button>
            </div>
            
            <div class="wiki-content">
              <MdPreview 
                editorId="wiki-preview" 
                :modelValue="currentPage.content || ''" 
                language="en-US" 
              />
            </div>
          </div>

          <div v-else class="wiki-edit-mode">
            <div class="edit-header">
              <input v-model="editTitle" type="text" class="edit-title-input" placeholder="Tiêu đề trang..." />
            </div>

            <div class="markdown-editor-wrapper">
              <MdEditor 
                v-model="editContent" 
                language="en-US" 
                class="advanced-markdown-editor" 
              />
            </div>

            <div class="edit-footer">
              <label class="wiki-visibility-toggle">
                <select v-model="editVisibility">
                  <option value="public">Công khai</option>
                  <option value="customer_safe">Cho khách hàng</option>
                  <option value="internal">Nội bộ</option>
                </select>
              </label>
              <div class="edit-actions">
                <button class="primary-button" @click="saveEdit" :disabled="!editTitle.trim()">
                  <Check :size="16" /> Lưu thay đổi
                </button>
                <button class="secondary-button" @click="cancelEdit">
                  <X :size="16" /> Hủy
                </button>
              </div>
            </div>
          </div>
        </div>
        
        <!-- Table of Contents Sidebar -->
        <div v-if="showWikiSidebar" class="wiki-sidebar">
          <div class="toc-container glass-card">
            <h3 class="toc-title">Mục lục</h3>
            <MdCatalog editorId="wiki-preview" :scrollElement="scrollElement" />
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.wiki-detail-page {
  width: 100%;
  min-height: 100vh;
  padding: 24px 0;
  background-color: var(--bg-main);
  display: flex;
  justify-content: center;
}

.wiki-detail-container {
  width: 100%;
  max-width: 1100px;
  padding: 0 24px;
  transition: max-width 0.3s;
}

.wiki-detail-container.editing-mode {
  max-width: 1400px; /* Expand wider for editor */
}

.wiki-layout {
  display: flex;
  gap: 24px;
  align-items: flex-start;
}

.wiki-top-nav {
  margin-bottom: 20px;
}

.back-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  background: transparent;
  border: none;
  color: var(--muted);
  font-weight: 600;
  font-size: 14px;
  cursor: pointer;
  padding: 8px 12px;
  border-radius: 8px;
  transition: all 0.2s ease;
}

.back-btn:hover {
  background: var(--bg-soft);
  color: var(--text-strong);
}

.wiki-document {
  flex: 1;
  background: var(--panel);
  border-radius: 12px;
  padding: 40px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05);
  min-height: 600px;
  min-width: 0; /* Prevent flex overflow */
}

.wiki-sidebar {
  width: 280px;
  flex-shrink: 0;
  position: sticky;
  top: 24px;
}

.toc-container {
  background: var(--panel);
  border-radius: 12px;
  padding: 20px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05);
  max-height: calc(100vh - 48px);
  overflow-y: auto;
}

.toc-title {
  font-size: 16px;
  font-weight: 700;
  margin-bottom: 12px;
  color: var(--text-strong);
  border-bottom: 1px solid var(--line);
  padding-bottom: 8px;
}

.wiki-header {
  border-bottom: 1px solid var(--line);
  padding-bottom: 24px;
  margin-bottom: 32px;
  position: relative;
}

.wiki-title {
  font-size: 32px;
  font-weight: 800;
  color: var(--text-strong);
  margin: 0 0 12px 0;
  line-height: 1.2;
}

.wiki-meta {
  display: flex;
  align-items: center;
  gap: 16px;
}

.wiki-author {
  color: var(--muted);
  font-size: 14px;
}

.wiki-badge {
  font-size: 12px;
  font-weight: 600;
  padding: 4px 10px;
  border-radius: 99px;
}

.wiki-badge.badge-public {
  color: var(--green-700);
  background: var(--green-50);
}

.wiki-badge.badge-internal {
  color: var(--slate-700);
  background: var(--slate-100);
}

.wiki-badge.badge-customer_safe {
  color: var(--blue-700);
  background: var(--blue-50);
}

.edit-btn {
  position: absolute;
  top: 0;
  right: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

/* Edit Mode Styles */
.edit-title-input {
  width: 100%;
  font-size: 28px;
  font-weight: 800;
  padding: 12px 16px;
  margin-bottom: 24px;
  border: 1px solid var(--line);
  border-radius: 8px;
  background: var(--bg-soft);
  color: var(--text-strong);
  outline: none;
  transition: border-color 0.2s;
}

.edit-title-input:focus {
  border-color: var(--primary);
  background: var(--panel);
}

.markdown-editor-wrapper {
  margin-bottom: 24px;
}

.advanced-markdown-editor {
  height: 600px;
}

.edit-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-top: 16px;
  border-top: 1px solid var(--line);
}

.edit-actions {
  display: flex;
  gap: 12px;
}

.edit-actions .primary-button {
  display: flex;
  align-items: center;
  gap: 8px;
}

/* Override md-editor-v3 defaults to match theme */
:deep(.md-editor) {
  --md-bk-color: var(--panel);
  --md-color: var(--text-strong);
}
</style>
