<script setup lang="ts">
import { computed, ref } from "vue";
import { useRouter, useRoute } from "vue-router";
import {
  FileText,
  Plus,
  Pencil,
  Trash2,
  Search,
  Globe2,
  Lock,
  Eye,
  Edit3
} from "lucide-vue-next";
import { useDashboardContext } from "../composables/dashboard-context";
import type { WikiPageDto } from "../types";
import { MdEditor } from 'md-editor-v3';
import 'md-editor-v3/lib/style.css';

const props = defineProps<{
  projectName: string;
  isAdmin: boolean;
}>();

const router = useRouter();
const route = useRoute();

const {
  wikiPages,
  createWikiPage,
  updateWikiPage,
  deleteWikiPage,
  formatDate,
} = useDashboardContext();

const showAddForm = ref(false);
const newPageTitle = ref("");
const newPageContent = ref("");
const newPageVisibility = ref("internal");
const wikiSearch = ref("");
const editingPageId = ref<string | null>(null);
const activeEditorTab = ref<'write' | 'preview'>('write');

const filteredWikiPages = computed(() => {
  const query = wikiSearch.value.trim().toLowerCase();
  if (!query) return wikiPages.value;

  return wikiPages.value.filter(
    (page: WikiPageDto) =>
      page.title.toLowerCase().includes(query) ||
      page.content.toLowerCase().includes(query),
  );
});

async function handleCreate() {
  const title = newPageTitle.value.trim();
  if (!title) return;

  const saved = editingPageId.value
    ? await updateWikiPage(
        editingPageId.value,
        title,
        newPageContent.value,
        newPageVisibility.value,
      )
    : await createWikiPage(
        title,
        newPageContent.value,
        newPageVisibility.value,
      );

  if (saved) {
    newPageTitle.value = "";
    newPageContent.value = "";
    newPageVisibility.value = "internal";
    editingPageId.value = null;
    showAddForm.value = false;
    activeEditorTab.value = 'write';
  }
}

function startEdit(page: WikiPageDto) {
  editingPageId.value = page.id;
  newPageTitle.value = page.title;
  newPageContent.value = page.content;
  newPageVisibility.value = (page as any).visibility ?? "internal";
  showAddForm.value = true;
  activeEditorTab.value = 'write';
}

function cancelEditor() {
  showAddForm.value = false;
  editingPageId.value = null;
  newPageTitle.value = "";
  newPageContent.value = "";
  newPageVisibility.value = "internal";
  activeEditorTab.value = 'write';
}

function toggleAddForm() {
  if (showAddForm.value) {
    cancelEditor();
    return;
  }
  showAddForm.value = true;
  activeEditorTab.value = 'write';
}

async function handleDelete(id: string) {
  if (!confirm("Bạn có chắc chắn muốn xóa trang Wiki này?")) return;
  await deleteWikiPage(id);
}

function navigateToWiki(page: WikiPageDto) {
  router.push({
    name: 'project-wiki-detail',
    params: { projectId: route.params.projectId, wikiId: page.id }
  });
}
</script>

<template>
  <div class="wiki-tab-content glass-card">
    <div class="panel-heading">
      <div>
        <span class="panel-heading__eyebrow">Knowledge Base</span>
        <h2>{{ projectName }} Wiki</h2>
      </div>
      <button
        class="primary-button primary-button--compact"
        type="button"
        @click="toggleAddForm"
      >
        <Plus :size="16" />
        <span>Tạo trang mới</span>
      </button>
    </div>

    <div v-if="showAddForm" class="wiki-add-form glass-card reveal">
      <h3>{{ editingPageId ? "Sửa trang Wiki" : "Tạo trang Wiki mới" }}</h3>
      <div class="wiki-editor">
        <input
          v-model="newPageTitle"
          type="text"
          placeholder="Tiêu đề trang..."
          @keyup.enter="handleCreate"
        />
        
        <MdEditor 
          v-model="newPageContent" 
          language="en-US"
          class="advanced-markdown-editor"
        />
        
        <div class="wiki-editor__footer">
          <label class="wiki-visibility-toggle">
            <select v-model="newPageVisibility">
              <option value="public">Công khai</option>
              <option value="customer_safe">Cho khách hàng</option>
              <option value="internal">Nội bộ</option>
            </select>
          </label>
          <div class="wiki-editor__actions">
            <button
              class="primary-button"
              type="button"
              :disabled="!newPageTitle.trim()"
              @click="handleCreate"
            >
              {{ editingPageId ? "Cập nhật" : "Tạo" }}
            </button>
            <button class="text-button" type="button" @click="cancelEditor">
              Hủy
            </button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="wikiPages.length > 0" class="wiki-list">
      <div class="wiki-search-bar">
        <Search :size="16" />
        <input
          v-model="wikiSearch"
          type="text"
          placeholder="Tìm kiếm trang Wiki..."
        />
      </div>

      <article
        v-for="page in filteredWikiPages"
        :key="page.id"
        class="wiki-item wiki-item--clickable"
        @click="navigateToWiki(page)"
      >
        <div class="wiki-item__icon">
          <FileText :size="20" />
        </div>
        <div class="wiki-item__main">
          <div class="wiki-item__title-row">
            <strong>{{ page.title }}</strong>
            <span
              class="wiki-visibility-badge"
              :class="{ 'is-public': (page as any).visibility === 'public' }"
            >
              <Globe2 v-if="(page as any).visibility === 'public'" :size="13" />
              <Lock v-else :size="13" />
              {{
                (page as any).visibility === "public"
                  ? "Công khai"
                  : (page as any).visibility === "customer_safe"
                    ? "Cho khách hàng"
                    : "Nội bộ"
              }}
            </span>
          </div>
          <span class="wiki-item__meta"
            >Cập nhật bởi {{ page.authorName }} vào
            {{ formatDate(page.updatedAt) }}</span
          >
        </div>
        <div v-if="isAdmin" class="wiki-item__actions" @click.stop>
          <button
            class="icon-button icon-button--small"
            type="button"
            title="Sửa nhanh"
            @click="startEdit(page)"
          >
            <Pencil :size="14" />
          </button>
          <button
            class="icon-button icon-button--small risk"
            type="button"
            title="Xóa"
            @click="handleDelete(page.id)"
          >
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
      <p>
        Wiki là nơi lưu trữ kiến thức dự án, guideline và quy trình làm việc của
        team.
      </p>
      <button
        class="secondary-button"
        type="button"
        style="margin-top: 16px"
        @click="toggleAddForm"
      >
        Bắt đầu viết Wiki
      </button>
    </div>
  </div>
</template>

<style scoped>
.wiki-tab-content {
  padding: 24px;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: var(--radius-shell);
}

.wiki-tab-content :deep(.primary-button),
.wiki-tab-content :deep(.primary-button span) {
  color: #fff;
}

.wiki-add-form {
  padding: 24px;
  margin-bottom: 24px;
  background: var(--blue-50);
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
}

.wiki-add-form h3 {
  font-size: 16px;
  margin-bottom: 16px;
  color: var(--text-strong);
  font-weight: 700;
}

.wiki-add-form input {
  width: 100%;
  padding: 10px 14px;
  margin-bottom: 12px;
  border-radius: var(--qaly-radius-lg);
  border: 1px solid var(--line);
  color: var(--text-strong);
  background: var(--panel);
  font-family: inherit;
}

.markdown-editor {
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  background: var(--panel);
  margin-bottom: 16px;
  overflow: hidden;
}

.markdown-tabs {
  display: flex;
  background: var(--bg-soft);
  border-bottom: 1px solid var(--line);
  padding: 0 8px;
}

.markdown-tab {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px 16px;
  border: none;
  background: transparent;
  color: var(--muted);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  border-bottom: 2px solid transparent;
  transition: all 0.2s;
}

.markdown-tab:hover {
  color: var(--text-strong);
}

.markdown-tab.active {
  color: var(--primary);
  border-bottom-color: var(--primary);
}

.markdown-textarea {
  width: 100%;
  padding: 16px;
  border: none;
  background: transparent;
  color: var(--text-strong);
  font-family: 'Consolas', 'Monaco', monospace;
  font-size: 14px;
  resize: vertical;
  min-height: 200px;
  line-height: 1.6;
  outline: none;
}

.markdown-preview-pane {
  padding: 16px;
  min-height: 200px;
  background: var(--panel);
  color: var(--text-strong);
}

.wiki-editor__footer {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: center;
}

.wiki-editor__actions {
  display: flex;
  gap: 10px;
  align-items: center;
}

.wiki-visibility-toggle {
  display: inline-flex;
  align-items: center;
  cursor: pointer;
}

.wiki-visibility-toggle input {
  width: 1px;
  height: 1px;
  opacity: 0;
  position: absolute;
}

.wiki-visibility-toggle span,
.wiki-visibility-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 28px;
  padding: 5px 10px;
  border-radius: 999px;
  border: 1px solid var(--line);
  color: var(--muted);
  background: var(--bg-soft);
  font-size: 12px;
  font-weight: 700;
}

.wiki-visibility-toggle input:checked + span,
.wiki-visibility-badge.is-public {
  color: var(--green-700);
  border-color: var(--green-200);
  background: var(--green-50);
}

.wiki-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.wiki-search-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 24px;
  padding: 12px 16px;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
}

.wiki-search-bar input {
  flex: 1;
  border: none;
  background: transparent;
  color: var(--text-strong);
  outline: none;
}

.wiki-item {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 14px 18px;
  border-radius: var(--qaly-radius-lg);
  background: var(--bg-soft);
  border: 1px solid var(--line);
  transition: transform 0.2s, box-shadow 0.2s, border-color 0.2s;
}

.wiki-item--clickable {
  cursor: pointer;
}

.wiki-item:hover {
  transform: translateY(-2px);
  box-shadow: var(--qaly-shadow-md);
  border-color: rgba(117, 182, 255, 0.4);
}

.wiki-item__icon {
  color: var(--primary);
}

.wiki-item__main {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
}

.wiki-item__title-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.wiki-item__main strong {
  font-size: 15px;
  color: var(--text-strong);
}

.wiki-item__meta {
  font-size: 12px;
  color: var(--muted);
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
  border-radius: var(--qaly-radius-lg);
  background: var(--primary);
  color: #f8fafc;
  display: grid;
  place-items: center;
  margin-bottom: 8px;
}

.wiki-empty h3 {
  font-size: 18px;
  margin: 0;
  color: var(--text-strong);
  font-weight: 700;
}

.wiki-empty p {
  color: var(--muted);
  max-width: 400px;
  margin: 0;
  font-size: 14px;
}

.wiki-item__actions {
  display: flex;
  gap: 8px;
  align-items: center;
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
  color: var(--text-strong);
}

.panel-heading__eyebrow {
  color: var(--muted);
  font-size: 13px;
  font-weight: 600;
  text-transform: uppercase;
}

.icon-button.risk:hover {
  color: var(--red-600);
  background: var(--red-50);
}
</style>
