<template>
  <div class="calendar-view">
    <div class="page-header">
      <h1>Calendar</h1>
      <div class="calendar-actions">
        <button class="btn btn-secondary" @click="previousMonth">
          <i class="icon-prev"></i>
        </button>
        <span class="current-month">{{ currentMonthYear }}</span>
        <button class="btn btn-secondary" @click="nextMonth">
          <i class="icon-next"></i>
        </button>
      </div>
    </div>

    <div class="calendar-grid">
      <div class="calendar-header">
        <div v-for="day in weekDays" :key="day" class="day-header">{{ day }}</div>
      </div>
      <div class="calendar-body">
        <div
          v-for="date in calendarDates"
          :key="date.date"
          :class="['calendar-day', { 'other-month': !date.currentMonth, today: date.isToday }]"
        >
          <div class="day-number">{{ date.day }}</div>
          <div class="day-episodes">
            <div
              v-for="episode in date.episodes"
              :key="episode.id"
              class="episode-dot"
              :title="episode.title"
            ></div>
          </div>
        </div>
      </div>
    </div>

    <div class="upcoming-episodes">
      <h2>Upcoming Episodes</h2>
      <div class="episode-list">
        <div v-for="episode in upcomingEpisodes" :key="episode.id" class="episode-item">
          <div class="episode-date">{{ formatDate(episode.airDate) }}</div>
          <div class="episode-info">
            <h4>{{ episode.series }}</h4>
            <p>{{ episode.title }}</p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

interface Episode {
  id: string
  title: string
  series: string
  airDate: Date
}

interface CalendarDate {
  date: string
  day: number
  currentMonth: boolean
  isToday: boolean
  episodes: Episode[]
}

const currentDate = ref(new Date())
const weekDays = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']

const currentMonthYear = computed(() => {
  return currentDate.value.toLocaleDateString('en-US', { month: 'long', year: 'numeric' })
})

const calendarDates = computed(() => {
  const year = currentDate.value.getFullYear()
  const month = currentDate.value.getMonth()
  const firstDay = new Date(year, month, 1)
  const startDate = new Date(firstDay)
  startDate.setDate(startDate.getDate() - firstDay.getDay())

  const dates: CalendarDate[] = []
  const currentDateObj = new Date(startDate)

  for (let i = 0; i < 42; i++) {
    const isCurrentMonth = currentDateObj.getMonth() === month
    const isToday = currentDateObj.toDateString() === new Date().toDateString()

    dates.push({
      date: currentDateObj.toISOString(),
      day: currentDateObj.getDate(),
      currentMonth: isCurrentMonth,
      isToday,
      episodes: [] as Episode[], // Would be populated with actual episodes
    })

    currentDateObj.setDate(currentDateObj.getDate() + 1)
  }

  return dates
})

const upcomingEpisodes = ref([
  {
    id: '1',
    series: 'The Joe Rogan Experience',
    title: 'Episode #1985',
    airDate: new Date(Date.now() + 24 * 60 * 60 * 1000),
  },
  {
    id: '2',
    series: 'This American Life',
    title: 'Episode #790',
    airDate: new Date(Date.now() + 2 * 24 * 60 * 60 * 1000),
  },
])

const previousMonth = () => {
  currentDate.value = new Date(currentDate.value.getFullYear(), currentDate.value.getMonth() - 1, 1)
}

const nextMonth = () => {
  currentDate.value = new Date(currentDate.value.getFullYear(), currentDate.value.getMonth() + 1, 1)
}

const formatDate = (date: Date): string => {
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}
</script>

<style scoped>
.calendar-view {
  padding: 0;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

.page-header h1 {
  margin: 0;
  color: white;
  font-size: 2rem;
}

.calendar-actions {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.current-month {
  color: white;
  font-weight: 600;
  min-width: 150px;
  text-align: center;
}

.btn {
  padding: 0.5rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  background-color: #555;
  color: white;
}

.calendar-grid {
  background-color: #2a2a2a;
  border-radius: 6px;
  overflow: hidden;
  margin-bottom: 2rem;
}

.calendar-header {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  background-color: #3a3a3a;
}

.day-header {
  padding: 1rem;
  text-align: center;
  color: white;
  font-weight: 600;
  border-right: 1px solid #555;
}

.calendar-body {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
}

.calendar-day {
  min-height: 80px;
  padding: 0.5rem;
  border-right: 1px solid #555;
  border-bottom: 1px solid #555;
  background-color: #2a2a2a;
}

.calendar-day.other-month {
  background-color: #1a1a1a;
  color: #666;
}

.calendar-day.today {
  background-color: #007acc;
}

.day-number {
  color: white;
  font-weight: 600;
  margin-bottom: 0.5rem;
}

.day-episodes {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
}

.episode-dot {
  width: 8px;
  height: 8px;
  background-color: #f39c12;
  border-radius: 50%;
}

.upcoming-episodes {
  background-color: #2a2a2a;
  border-radius: 6px;
  padding: 1.5rem;
}

.upcoming-episodes h2 {
  color: white;
  margin-bottom: 1rem;
}

.episode-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.episode-item {
  display: flex;
  gap: 1rem;
  padding: 1rem;
  background-color: #3a3a3a;
  border-radius: 6px;
}

.episode-date {
  color: #f39c12;
  font-weight: 600;
  min-width: 60px;
}

.episode-info h4 {
  color: white;
  margin: 0 0 0.25rem 0;
}

.episode-info p {
  color: #ccc;
  margin: 0;
}

.icon-prev::before {
  content: '◀';
}
.icon-next::before {
  content: '▶';
}
</style>
