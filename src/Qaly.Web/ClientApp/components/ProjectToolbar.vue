<script setup lang="ts">
import { LayoutGrid, List, Plus, Search, SlidersHorizontal, X } from 'lucide-vue-next'

defineProps<{
  search: string
  sort: string
  filter: string
  projectCount: number
  isGridView: boolean
}>()

defineEmits<{
  'update:search': [value: string]
  'update:sort': [value: string]
  'update:filter': [value: string]
  'update:isGridView': [value: boolean]
  create: []
}>()
</script>

<template>
  <div class="project-toolbar">
    <div class="project-toolbar__top">
      <label class="project-search" aria-label="Tìm kiếm dự án">
        <Search :size="17" />
        <input
          :value="search"
          type="search"
          placeholder="Tìm dự án, chủ sở hữu, mô tả..."
          @input="$emit('update:search', ($event.target as HTMLInputElement).value)"
        />
        <button
          v-if="search"
          type="button"
          aria-label="Xóa tìm kiếm"
          @click="$emit('update:search', '')"
        >
          <X :size="15" />
        </button>
      </label>

      <div class="project-toolbar__meta">
        <span class="project-toolbar__count">{{ projectCount }} dự án</span>
      </div>
    </div>

    <div class="project-toolbar__bottom">
      <div class="project-toolbar__filters">
        <label>
          <SlidersHorizontal :size="15" />
          <select
            :value="filter"
            aria-label="Lọc dự án"
            @change="$emit('update:filter', ($event.target as HTMLSelectElement).value)"
          >
            <option value="all">Tất cả</option>
            <option value="active">Đang chạy</option>
            <option value="planned">Đã lên kế hoạch</option>
            <option value="at-risk">Có rủi ro</option>
          </select>
        </label>

        <select
          :value="sort"
          aria-label="Sắp xếp dự án"
          @change="$emit('update:sort', ($event.target as HTMLSelectElement).value)"
        >
          <option value="recent">Mới nhất</option>
          <option value="risk">Rủi ro trước</option>
          <option value="progress">Tiến độ cao</option>
          <option value="name">Tên A-Z</option>
        </select>
      </div>

      <div class="project-toolbar__actions">
        <button
          class="layout-toggle-btn"
          type="button"
          :title="
            isGridView ? 'Chuyển sang dạng danh sách' : 'Chuyển sang dạng ô lưới'
          "
          @click="$emit('update:isGridView', !isGridView)"
        >
          <List v-if="isGridView" :size="16" />
          <LayoutGrid v-else :size="16" />
        </button>

        <button class="primary-button primary-button--compact" type="button" @click="$emit('create')">
          <Plus :size="16" />
          <span>Tạo dự án</span>
        </button>

        <slot name="actions" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.project-toolbar {
  display: grid;
  gap: 12px;
  padding: 14px;
  border: 1px solid rgba(226, 232, 240, 0.92);
  border-radius: 18px;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(248, 250, 252, 0.96));
  box-shadow:
    0 6px 18px rgba(15, 23, 42, 0.03),
    inset 0 1px 0 rgba(255, 255, 255, 0.85);
}

.project-toolbar__top,
.project-toolbar__bottom {
  display: flex;
  align-items: center;
  gap: 12px;
  justify-content: space-between;
}

.project-toolbar__bottom {
  flex-wrap: wrap;
}

.project-search {
  flex: 1 1 380px;
  min-width: 0;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 10px;
  min-height: 48px;
  padding: 0 12px;
  border: 1px solid rgba(203, 213, 225, 0.95);
  border-radius: 14px;
  background: #f8fafc;
}

.project-search svg {
  color: #64748b;
}

.project-search input {
  width: 100%;
  border: 0;
  outline: 0;
  color: #0f172a;
  background: transparent;
}

.project-search input::placeholder {
  color: #94a3b8;
}

.project-search button {
  width: 32px;
  height: 32px;
  display: grid;
  place-items: center;
  border: 0;
  border-radius: 999px;
  color: #64748b;
  background: transparent;
}

.project-toolbar__meta {
  display: flex;
  align-items: center;
  justify-content: flex-end;
}

.project-toolbar__count {
  display: inline-flex;
  align-items: center;
  min-height: 34px;
  padding: 0 12px;
  border: 1px solid rgba(191, 219, 254, 0.7);
  border-radius: 999px;
  color: #1d4ed8;
  background: #eff6ff;
  font-size: 12px;
  font-weight: 800;
}

.project-toolbar__filters,
.project-toolbar__actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
}

.project-toolbar__filters {
  flex: 1 1 auto;
}

.project-toolbar__filters label,
.project-toolbar__filters select {
  min-height: 40px;
}

.project-toolbar__filters label,
.project-toolbar__filters select,
.layout-toggle-btn {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 0 12px;
  border: 1px solid rgba(203, 213, 225, 0.95);
  border-radius: 12px;
  color: #475569;
  background: #ffffff;
  font-weight: 700;
}

.project-toolbar__filters label {
  background: #f8fafc;
}

.project-toolbar__filters select {
  padding-right: 10px;
}

.layout-toggle-btn {
  min-height: 40px;
  justify-content: center;
  width: 44px;
  padding: 0;
}

.primary-button--compact {
  min-height: 40px;
  padding: 0 14px;
}

@media (max-width: 960px) {
  .project-toolbar__top,
  .project-toolbar__bottom {
    align-items: stretch;
    flex-direction: column;
  }

  .project-toolbar__meta {
    justify-content: flex-start;
  }

  .project-toolbar__filters,
  .project-toolbar__actions {
    width: 100%;
  }

  .project-toolbar__filters label,
  .project-toolbar__filters select,
  .layout-toggle-btn,
  .primary-button--compact {
    flex: 1 1 auto;
  }
}
</style>

<style scoped>
.project-toolbar {
  gap: 14px;
  padding: 16px;
  border-color: rgba(226, 232, 240, 0.92);
  border-radius: 20px;
  box-shadow:
    0 16px 34px rgba(15, 23, 42, 0.06),
    inset 0 1px 0 rgba(255, 255, 255, 0.9);
}

.project-toolbar::before {
  content: '';
  position: absolute;
  inset: 0 auto auto 0;
  width: 100%;
  height: 4px;
  background: linear-gradient(90deg, #fbbf24, #2dd4bf);
}

.project-search {
  padding: 0 14px;
  border-radius: 16px;
  background: #ffffff;
}

.project-toolbar__count {
  border-color: rgba(251, 191, 36, 0.22);
  color: #92400e;
  background: rgba(254, 243, 199, 0.88);
}

.project-toolbar__filters label,
.project-toolbar__filters select,
.layout-toggle-btn {
  border-color: rgba(203, 213, 225, 0.95);
  border-radius: 12px;
}

.layout-toggle-btn,
.primary-button--compact,
.project-toolbar__actions .btn-toolbar {
  min-height: 40px;
}

.btn-toolbar--accent {
  border-color: rgba(251, 191, 36, 0.22);
  color: #92400e;
  background: rgba(254, 243, 199, 0.88);
}

.btn-toolbar--accent:hover {
  color: #78350f;
  border-color: rgba(245, 158, 11, 0.28);
  background: rgba(253, 230, 138, 0.95);
}

@media (max-width: 960px) {
  .project-toolbar__top,
  .project-toolbar__bottom {
    align-items: stretch;
    flex-direction: column;
  }
}
</style>
