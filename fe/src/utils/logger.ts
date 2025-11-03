/**
 * Simple logger utility for consistent logging across the application
 * Logs are only output in development mode by default
 */

const isDev = import.meta.env.DEV

export const logger = {
  /**
   * Log debug information (only in development)
   */
  debug: (message: string, ...args: unknown[]) => {
    if (isDev) {
      console.log(`[DEBUG] ${message}`, ...args)
    }
  },

  /**
   * Log general information
   */
  info: (message: string, ...args: unknown[]) => {
    if (isDev) {
      console.info(`[INFO] ${message}`, ...args)
    }
  },

  /**
   * Log warnings
   */
  warn: (message: string, ...args: unknown[]) => {
    console.warn(`[WARN] ${message}`, ...args)
  },

  /**
   * Log errors
   */
  error: (message: string, ...args: unknown[]) => {
    console.error(`[ERROR] ${message}`, ...args)
  },

  /**
   * Log with a custom prefix/category
   */
  log: (category: string, message: string, ...args: unknown[]) => {
    if (isDev) {
      console.log(`[${category}] ${message}`, ...args)
    }
  }
}
