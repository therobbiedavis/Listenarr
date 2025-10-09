import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'home',
      component: () => import('../views/AudiobooksView.vue'),
    },
    {
      path: '/audiobooks',
      name: 'audiobooks',
      component: () => import('../views/AudiobooksView.vue'),
    },
    {
      path: '/audiobooks/:id',
      name: 'audiobook-detail',
      component: () => import('../views/AudiobookDetailView.vue'),
    },
    {
      path: '/add-new',
      name: 'add-new',
      component: () => import('../views/AddNewView.vue'),
    },
    {
      path: '/library-import',
      name: 'library-import',
      component: () => import('../views/LibraryImportView.vue'),
    },
    // Calendar route temporarily disabled
    // {
    //   path: '/calendar',
    //   name: 'calendar',
    //   component: () => import('../views/CalendarView.vue'),
    // },
    {
      path: '/activity',
      name: 'activity',
      component: () => import('../views/ActivityView.vue'),
    },
    {
      path: '/wanted',
      name: 'wanted',
      component: () => import('../views/WantedView.vue'),
    },
    {
      path: '/downloads',
      name: 'downloads',
      component: () => import('../views/DownloadsView.vue'),
    },
    {
      path: '/settings',
      name: 'settings',
      component: () => import('../views/SettingsView.vue'),
    },
    {
      path: '/system',
      name: 'system',
      component: () => import('../views/SystemView.vue'),
    },
    {
      path: '/logs',
      name: 'logs',
      component: () => import('../views/LogsView.vue'),
    },
  ],
})

export default router
