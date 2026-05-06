<script setup lang="ts">
import { computed } from 'vue'
import { CalendarDays, Mail, ShieldCheck, UserRound } from 'lucide-vue-next'
import { useDashboardContext } from '../composables/dashboard-context'

const { currentUser, displayRole, formatDate } = useDashboardContext()

const user = computed(() => currentUser.value)
const userInitials = computed(() => {
  const name = user.value?.fullName || user.value?.email || 'Qaly user'

  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part: string) => part[0]?.toUpperCase() ?? '')
    .join('')
})
</script>

<template>
  <div class="dashboard-scroll dashboard-scroll--embedded no-scrollbar">
    <div class="dashboard-main project-home-main no-scrollbar">
      <section class="profile-page glass-card">
        <header class="profile-page__header">
          <div class="profile-avatar profile-page__avatar">{{ userInitials }}</div>
          <div>
            <span>Profile</span>
            <h2>Trang cá nhân</h2>
            <p>{{ user?.fullName || user?.email || 'Qaly user' }}</p>
          </div>
        </header>

        <div class="profile-page__grid">
          <article class="profile-page__item">
            <UserRound :size="20" />
            <div>
              <span>Họ tên</span>
              <strong>{{ user?.fullName || user?.email || 'Qaly user' }}</strong>
            </div>
          </article>

          <article class="profile-page__item">
            <Mail :size="20" />
            <div>
              <span>Email</span>
              <strong>{{ user?.email ?? 'Chưa có email' }}</strong>
            </div>
          </article>

          <article class="profile-page__item">
            <ShieldCheck :size="20" />
            <div>
              <span>Vai trò</span>
              <strong>{{ displayRole(user?.role) || 'Member' }}</strong>
            </div>
          </article>

          <article class="profile-page__item">
            <CalendarDays :size="20" />
            <div>
              <span>Ngày tham gia</span>
              <strong>{{ user?.createdAt ? formatDate(user.createdAt) : 'No date' }}</strong>
            </div>
          </article>
        </div>

        <div class="profile-page__status" :class="{ 'is-active': user?.isActive }">
          {{ user?.isActive ? 'Đang hoạt động' : 'Tạm khóa' }}
        </div>
      </section>
    </div>
  </div>
</template>
