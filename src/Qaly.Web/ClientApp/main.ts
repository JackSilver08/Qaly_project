import { createApp } from 'vue'
import App from './App.vue'
import './style.css'

const target = document.getElementById('qaly-dashboard-app')

if (target) {
  createApp(App).mount(target)
}
