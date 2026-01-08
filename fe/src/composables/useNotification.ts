/*
 * Listenarr - Audiobook Management System
 * Copyright (C) 2024-2025 Robbie Davis
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

import { ref } from 'vue'

export type NotificationType = 'success' | 'error' | 'warning' | 'info'

interface NotificationState {
  visible: boolean
  message: string
  title?: string
  type: NotificationType
  autoClose: number
}

const notificationState = ref<NotificationState>({
  visible: false,
  message: '',
  type: 'info',
  autoClose: 3000,
})

export function useNotification() {
  const show = (
    message: string,
    type: NotificationType = 'info',
    title?: string,
    autoClose: number = 3000,
  ) => {
    notificationState.value = {
      visible: true,
      message,
      title,
      type,
      autoClose,
    }
  }

  const success = (message: string, title: string = 'Success', autoClose: number = 3000) => {
    show(message, 'success', title, autoClose)
  }

  const error = (message: string, title: string = 'Error', autoClose: number = 0) => {
    show(message, 'error', title, autoClose)
  }

  const warning = (message: string, title: string = 'Warning', autoClose: number = 4000) => {
    show(message, 'warning', title, autoClose)
  }

  const info = (message: string, title?: string, autoClose: number = 3000) => {
    show(message, 'info', title, autoClose)
  }

  const close = () => {
    notificationState.value.visible = false
  }

  return {
    notification: notificationState,
    show,
    success,
    error,
    warning,
    info,
    close,
  }
}
