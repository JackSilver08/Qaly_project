<script setup lang="ts">
import { Plus, Search, SlidersHorizontal, X } from 'lucide-vue-next'

defineProps<{
  search: string
  sort: string
  filter: string
  projectCount: number
}>()

defineEmits<{
  'update:search': [value: string]
  'update:sort': [value: string]
  'update:filter': [value: string]
  create: []
}>()
</script>

<template>
  <div class="project-toolbar">
    <label class="project-search" aria-label="Tìm kiếm dự án">
      <Search :size="17" />
      <input
        :value="search"
        type="search"
        placeholder="Tìm dự án, chủ sở hữu, mô tả..."
        @input="$emit('update:search', ($event.target as HTMLInputElement).value)"
      />
      <button v-if="search" type="button" aria-label="Xóa tìm kiếm" @click="$emit('update:search', '')">
        <X :size="15" />
      </button>
    </label>

    <div class="project-toolbar__filters">
      <label>
        <SlidersHorizontal :size="15" />
        <select :value="filter" aria-label="Lọc dự án" @change="$emit('update:filter', ($event.target as HTMLSelectElement).value)">
          <option value="all">Tất cả</option>
          <option value="active">Đang chạy</option>
          <option value="planned">Đã lên kế hoạch</option>
          <option value="at-risk">Có rủi ro</option>
        </select>
      </label>

      <select :value="sort" aria-label="Sắp xếp dự án" @change="$emit('update:sort', ($event.target as HTMLSelectElement).value)">
        <option value="recent">Mới nhất</option>
        <option value="risk">Rủi ro trước</option>
        <option value="progress">Tiến độ cao</option>
        <option value="name">Tên A-Z</option>
      </select>

      <button class="primary-button primary-button--compact" type="button" @click="$emit('create')">
        <Plus :size="16" />
        <span>Tạo dự án</span>
      </button>
    </div>

    <span class="project-toolbar__count">{{ projectCount }} dự án</span>
  </div>
</template>
