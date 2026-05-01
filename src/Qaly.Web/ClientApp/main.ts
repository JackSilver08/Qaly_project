import { createApp } from 'vue'
import './style.css'

// Ví dụ về "Island" component
// import KanbanBoard from './components/KanbanBoard.vue'

const app = createApp({})

// app.component('kanban-board', KanbanBoard)

// Mount app vào một element nếu tồn tại (ví dụ: <div id="app"></div> trong Razor Page)
if (document.getElementById('app')) {
  app.mount('#app')
}

console.log('Qaly Frontend Initialized')
