(async () => {
  try {
    const path = (await import('path')).default
    const vite = await import('vite')
    const cwd = process.cwd()
    const server = await vite.createServer({ root: path.resolve(cwd) })
    try {
      console.log('Attempting ssrLoadModule of /src/views/ActivityView.vue')
      const mod = await server.ssrLoadModule('/src/views/ActivityView.vue')
      console.log('ssrLoadModule succeeded:', Object.keys(mod).join(','))
    } catch (err) {
      console.error('ssrLoadModule error:', err && err.stack ? err.stack : err)
    } finally {
      await server.close()
    }
  } catch (e) {
    console.error('failed to create vite server or import module:', e && e.stack ? e.stack : e)
    process.exit(1)
  }
})()
