/**
 * Error Tracking Service
 * 
 * Centralized error tracking and logging service for production use.
 * In production, errors can be sent to external services (e.g., Sentry, LogRocket).
 * In development, errors are logged to the console for debugging.
 */

export interface ErrorContext {
  component?: string
  operation?: string
  userId?: string
  metadata?: Record<string, unknown>
}

export interface TrackedError {
  message: string
  stack?: string
  context?: ErrorContext
  timestamp: Date
}

class ErrorTrackingService {
  private errors: TrackedError[] = []
  private maxStoredErrors = 100

  /**
   * Capture an exception with optional context
   */
  captureException(error: Error | unknown, context?: ErrorContext): void {
    const trackedError: TrackedError = {
      message: error instanceof Error ? error.message : String(error),
      stack: error instanceof Error ? error.stack : undefined,
      context,
      timestamp: new Date()
    }

    // Store for potential retrieval
    this.errors.push(trackedError)
    if (this.errors.length > this.maxStoredErrors) {
      this.errors.shift()
    }

    // In development, log to console
    if (import.meta.env.DEV) {
      console.error('[ErrorTracking]', {
        error: trackedError.message,
        context: trackedError.context,
        stack: trackedError.stack
      })
    }

    // In production, send to external service (TODO: implement when ready)
    if (import.meta.env.PROD) {
      this.sendToExternalService(trackedError)
    }
  }

  /**
   * Capture a message with severity level
   */
  captureMessage(message: string, level: 'info' | 'warning' | 'error' = 'info', context?: ErrorContext): void {
    const trackedError: TrackedError = {
      message: `[${level.toUpperCase()}] ${message}`,
      context,
      timestamp: new Date()
    }

    this.errors.push(trackedError)
    if (this.errors.length > this.maxStoredErrors) {
      this.errors.shift()
    }

    if (import.meta.env.DEV) {
      const logFn = level === 'error' ? console.error : level === 'warning' ? console.warn : console.info
      logFn('[ErrorTracking]', message, context)
    }

    if (import.meta.env.PROD) {
      this.sendToExternalService(trackedError)
    }
  }

  /**
   * Get recent errors for debugging
   */
  getRecentErrors(count = 10): TrackedError[] {
    return this.errors.slice(-count)
  }

  /**
   * Clear stored errors
   */
  clearErrors(): void {
    this.errors = []
  }

  /**
   * Send error to external tracking service (e.g., Sentry)
   * TODO: Implement integration with external service when ready
   */
  private sendToExternalService(error: TrackedError): void {
    // Placeholder for external service integration
    // Example with Sentry:
    // Sentry.captureException(new Error(error.message), {
    //   contexts: {
    //     custom: error.context
    //   }
    // })
    
    // For now, just log that we would send it
    if (import.meta.env.DEV) {
      console.log('[ErrorTracking] Would send to external service:', error.message)
    }
  }

  /**
   * Set user context for error tracking
   */
  setUserContext(userId: string, email?: string, username?: string): void {
    // TODO: Implement user context tracking
    // Example with Sentry:
    // Sentry.setUser({ id: userId, email, username })
    
    if (import.meta.env.DEV) {
      console.log('[ErrorTracking] User context set:', { userId, email, username })
    }
  }

  /**
   * Add breadcrumb for debugging user actions
   */
  addBreadcrumb(message: string, category?: string, data?: Record<string, unknown>): void {
    // TODO: Implement breadcrumb tracking
    // Example with Sentry:
    // Sentry.addBreadcrumb({ message, category, data })
    
    if (import.meta.env.DEV) {
      console.log('[ErrorTracking] Breadcrumb:', { message, category, data })
    }
  }
}

// Export singleton instance
export const errorTracking = new ErrorTrackingService()

// Setup global error handler
if (typeof window !== 'undefined') {
  window.addEventListener('error', (event) => {
    errorTracking.captureException(event.error, {
      component: 'Global',
      operation: 'unhandledError'
    })
  })

  window.addEventListener('unhandledrejection', (event) => {
    errorTracking.captureException(event.reason, {
      component: 'Global',
      operation: 'unhandledRejection'
    })
  })
}
