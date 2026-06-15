<script setup lang="ts">
import { Users } from 'lucide-vue-next'
import type { TeamMiniMemberModel } from './dashboard-models'

defineProps<{
  members: TeamMiniMemberModel[]
}>()
</script>

<template>
  <section class="team-mini-section glass-card">
    <div class="panel-heading">
      <div>
        <span>Đội ngũ</span>
        <h2>Tổng quan đội ngũ</h2>
      </div>
      <Users :size="18" />
    </div>

    <div class="team-mini-grid">
      <article v-for="member in members" :key="member.id" class="team-mini-card">
        <span class="avatar-token avatar-token--ember">{{ member.initials }}</span>
        <div>
          <strong>{{ member.name }}</strong>
          <p>{{ member.role }} - {{ member.focusArea }}</p>
        </div>
        <div class="strip-progress">
          <span :style="{ width: `${member.capacityPercent}%` }"></span>
        </div>
      </article>
    </div>
  </section>
</template>
