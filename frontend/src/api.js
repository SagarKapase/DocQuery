// All calls to the backend live here.

function authHeader() {
  const token = localStorage.getItem('token')
  return token ? { Authorization: `Bearer ${token}` } : {}
}

async function handle(response) {
  if (response.status === 401) {
    localStorage.clear()
    window.location.reload()
  }
  if (!response.ok) throw new Error(await response.text())
  return response.status === 204 ? null : response.json()
}

export async function login(email, password, isRegister) {
  const url = isRegister ? '/api/auth/register' : '/api/auth/login'
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  return handle(response)
}

export async function getDocuments() {
  return handle(await fetch('/api/documents', { headers: authHeader() }))
}

export async function uploadDocument(file) {
  const form = new FormData()
  form.append('file', file)
  return handle(await fetch('/api/documents/upload', { method: 'POST', headers: authHeader(), body: form }))
}

export async function deleteDocument(id) {
  return handle(await fetch(`/api/documents/${id}`, { method: 'DELETE', headers: authHeader() }))
}

// Reads the Server-Sent Events stream from /api/chat/ask.
// onEvent is called for every {type: ...} object the server sends.
// (EventSource can't send POST bodies or auth headers, so we read the stream with fetch.)
export async function askQuestion(question, documentId, onEvent) {
  const response = await fetch('/api/chat/ask', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...authHeader() },
    body: JSON.stringify({ question, documentId }),
  })
  if (!response.ok) throw new Error(await response.text())

  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''

  while (true) {
    const { done, value } = await reader.read()
    if (done) break

    buffer += decoder.decode(value, { stream: true })

    // Events are separated by a blank line
    const parts = buffer.split('\n\n')
    buffer = parts.pop() // last part may be incomplete, keep it for next round

    for (const part of parts) {
      if (part.startsWith('data: ')) {
        onEvent(JSON.parse(part.slice(6)))
      }
    }
  }
}
