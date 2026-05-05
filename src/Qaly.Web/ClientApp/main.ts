import { createApp } from 'vue'
import App from './App.vue'
import { router } from './router'
import './style.css'

const target = document.getElementById('qaly-dashboard-app')

if (target) {
  createApp(App).use(router).mount(target)
}
