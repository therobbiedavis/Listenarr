describe('Settings UI - e2e', () => {
  beforeEach(() => {
    // Stub startup config to indicate authentication is NOT required so the
    // SPA won't redirect to the login page during tests.
    cy.intercept('GET', '/api/configuration/startupconfig', {
      statusCode: 200,
      body: {
        authenticationRequired: false,
        apiKey: null,
        baseUrl: '/',
      }
    }).as('getStartupConfig')

    // Some dev setups may request the startup config without the /api prefix; stub that too
    cy.intercept('GET', '/configuration/startupconfig', {
      statusCode: 200,
      body: {
        authenticationRequired: false,
        apiKey: null,
        baseUrl: '/',
      }
    }).as('getStartupConfigNoApi')

    // Stub account/me to return an unauthenticated but non-redirecting response
    // (the SPA treats this as not requiring a login here).
    cy.intercept('GET', '/api/account/me', {
      statusCode: 200,
      body: { authenticated: false }
    }).as('getCurrentUser')

      // Also stub account/me without /api in case the SPA requests a bare path
      cy.intercept('GET', '/account/me', {
        statusCode: 200,
        body: { authenticated: false }
      }).as('getCurrentUserNoApi')

    // Intercept the GET for application settings and return a baseline where outputPath is empty
    cy.intercept('GET', '/api/configuration/settings', {
      statusCode: 200,
      body: {
        preferUsDomain: false,
        useUsProxy: false,
        usProxyHost: '',
        usProxyPort: 0,
        usProxyUsername: '',
        usProxyPassword: '',
        outputPath: '',
        fileNamingPattern: '{Author}/{Title}',
        completedFileAction: 'Move',
        maxConcurrentDownloads: 2,
        pollingIntervalSeconds: 30,
      }
    }).as('getSettings')

    // Also accept the same settings path without /api
    cy.intercept('GET', '/configuration/settings', {
      statusCode: 200,
      body: {
        preferUsDomain: false,
        useUsProxy: false,
        usProxyHost: '',
        usProxyPort: 0,
        usProxyUsername: '',
        usProxyPassword: '',
        outputPath: '',
        fileNamingPattern: '{Author}/{Title}',
        completedFileAction: 'Move',
        maxConcurrentDownloads: 2,
        pollingIntervalSeconds: 30,
      }
    }).as('getSettingsNoApi')

    // Stub other startup endpoints the Settings page loads so Promise.all settles
    cy.intercept('GET', '/api/configuration/apis', { statusCode: 200, body: [] }).as('getApis')
    cy.intercept('GET', '/api/configuration/download-clients', { statusCode: 200, body: [] }).as('getDownloadClients')
    cy.intercept('GET', '/api/remotepath', { statusCode: 200, body: [] }).as('getRemotePathMappings')
    cy.intercept('GET', '/api/indexers', { statusCode: 200, body: [] }).as('getIndexers')
    cy.intercept('GET', '/api/qualityprofile', { statusCode: 200, body: [] }).as('getQualityProfiles')
    cy.intercept('GET', '/api/account/admins', { statusCode: 200, body: [] }).as('getAdminUsers')

    // Intercept save and assert payload
    cy.intercept('POST', '/api/configuration/settings', (req) => {
      req.reply((res) => {
        // Respond with the same payload to simulate persistence
        res.send({ statusCode: 200, body: req.body })
      })
    }).as('saveSettings')
  })

  // On failure, save the current page HTML and a screenshot to help diagnose
  // what the SPA rendered when the test failed.
  afterEach(function () {
    // Use function() to access `this.currentTest`
    if (this.currentTest && this.currentTest.state === 'failed') {
      const ts = Date.now()
      const htmlPath = `cypress/screenshots/failure-${ts}.html`
      const shotName = `failure-${ts}`
      // Write the full HTML document to the screenshots folder for debugging
      cy.document().then((doc) => {
        const html = doc.documentElement.outerHTML
        cy.writeFile(htmlPath, html)
        cy.log(`Wrote failure HTML to ${htmlPath}`)
      })
      // Also take a screenshot (Cypress will also capture one on failure but we ensure it)
      cy.screenshot(shotName)
    }
  })

  it('fills required fields, enables proxy, and saves settings', () => {
  // Visit the home page first to see if RouterView works
  cy.visit('/', { timeout: 10000 })

  // Check that the app is mounted
  cy.get('#app', { timeout: 10000 }).should('exist')
  cy.log('Home page loaded successfully')

  // Wait a bit for the page to stabilize
  cy.wait(2000)

  // Test navigation to another page first (add-new)
  cy.visit('/add-new', { timeout: 10000 })
  cy.url({ timeout: 10000 }).should('include', '/add-new')
  cy.log('Navigation to /add-new works')

  // Wait for navigation
  cy.wait(2000)

  // Navigate back to home and then click the settings link in the sidebar
  cy.visit('/', { timeout: 10000 })
  cy.wait(2000)

  // Click the settings link in the sidebar
  cy.contains('Settings').click()

  // Wait for navigation to complete
  cy.wait(3000)

  // Check that the app is still mounted
  cy.get('#app', { timeout: 10000 }).should('exist')
  cy.log('App still mounted after navigation to settings')

  // Assert the URL includes /settings
  cy.url({ timeout: 10000 }).should('include', '/settings')
  cy.log('URL includes /settings')

  // Wait more time for component to render
  cy.wait(2000)

  // Ensure the settings page is rendered
  cy.get('.main-content').should('exist')
  cy.log('Main content exists')

  // Check what's in the main content
  cy.get('.main-content').invoke('html').then(html => {
    cy.log('Main content HTML:', html.substring(0, 500))
  })

  cy.get('.settings-page').should('exist')

    // Click on the "General Settings" tab to show the form
    cy.contains('General Settings').click()

    // Wait for the tab to switch
    cy.wait(1000)

    // Ensure the general settings form is rendered by checking the output-path input
    // Increase timeout as the SPA may take longer to fetch required data.
    cy.get('input[placeholder="Select a folder for audiobooks..."]', { timeout: 20000 }).should('exist')


    // Output path: use the folder browser input
  // Use the folder browser input placeholder to locate the field reliably in the SPA
  cy.get('input[placeholder="Select a folder for audiobooks..."]').clear()
  cy.get('input[placeholder="Select a folder for audiobooks..."]').type('/mnt/audiobooks')

    // Enable proxy
    cy.contains('Use HTTP proxy for US requests').parent().find('input[type="checkbox"]').check()

    // Fill proxy host and port (select the port input by its label to avoid hitting other number inputs)
  // Use stable data-cy selectors for proxy host/port
  cy.get('[data-cy="us-proxy-host"]').clear()
  cy.get('[data-cy="us-proxy-host"]').type('proxy.test.local')
  cy.get('[data-cy="us-proxy-port"]').clear()
  cy.get('[data-cy="us-proxy-port"]').type('3128')

    // Click Save
    cy.contains('Save Settings').click()

    // Confirm save request was made with expected payload
    cy.wait('@saveSettings').its('request.body').then((body) => {
      expect(body.outputPath).to.equal('/mnt/audiobooks')
      expect(body.useUsProxy).to.equal(true)
      expect(body.usProxyHost).to.equal('proxy.test.local')
      expect(Number(body.usProxyPort)).to.equal(3128)
    })

    // Optionally assert UI shows success toast (depends on toast implementation)
    cy.contains('Settings saved successfully').should('exist')
  })
})
