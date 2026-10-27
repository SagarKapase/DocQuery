import { useEffect, useRef, useState } from 'react'
import Markdown from 'react-markdown'
import { askQuestion } from './api'

const SUGGESTIONS = ['Summarize this document', 'What are the key points?', 'List the main skills mentioned']

export default function Chat({ document }) {
  const [question, setQuestion] = useState('')
  const [messages, setMessages] = useState([]) // { question, answer, sources }
  const [loading, setLoading] = useState(false)
  const bottomRef = useRef(null)

  // Keep the newest message in view while the answer streams in
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  // Updates the last message (the one currently being answered)
  function updateLast(change) {
    setMessages((prev) => {
      const copy = [...prev]
      copy[copy.length - 1] = change(copy[copy.length - 1])
      return copy
    })
  }

  async function ask(text) {
    if (!text.trim() || loading) return

    setMessages((prev) => [...prev, { question: text, answer: '', sources: [] }])
    setQuestion('')
    setLoading(true)

    try {
      await askQuestion(text, document?.id ?? null, (event) => {
        if (event.type === 'sources') updateLast((m) => ({ ...m, sources: event.sources }))
        if (event.type === 'token') updateLast((m) => ({ ...m, answer: m.answer + event.text }))
        if (event.type === 'error') updateLast((m) => ({ ...m, answer: event.message }))
      })
    } catch (err) {
      updateLast((m) => ({ ...m, answer: 'Error: ' + err.message }))
    }
    setLoading(false)
  }

  function handleSubmit(e) {
    e.preventDefault()
    ask(question)
  }

  return (
    <section className="chat">
      <div className="chat-scope">
        Asking: <b>{document ? document.fileName : 'All documents'}</b>
      </div>

      <div className="messages">
        {messages.length === 0 && (
          <div className="empty">
            <span className="logo big">DQ</span>
            <h2>Ask anything about your documents</h2>
            <p className="muted">Answers come only from your PDFs, with page citations.</p>
            <div className="suggestions">
              {SUGGESTIONS.map((s) => (
                <button key={s} onClick={() => ask(s)}>{s}</button>
              ))}
            </div>
          </div>
        )}

        {messages.map((m, i) => (
          <div key={i} className="turn">
            <div className="bubble user">{m.question}</div>

            <div className="bot-row">
              <span className="bot-avatar">AI</span>
              <div className="bubble bot">
                {m.answer ? (
                  <div className="markdown"><Markdown>{m.answer}</Markdown></div>
                ) : (
                  <div className="typing"><span /><span /><span /></div>
                )}

                {m.sources.length > 0 && (
                  <details className="sources">
                    <summary>{m.sources.length} sources</summary>
                    {m.sources.map((s, j) => (
                      <div key={j} className="source">
                        <div className="source-head">
                          <span className="badge">{j + 1}</span>
                          <span className="source-file">{s.fileName}</span>
                          <span className="page">Page {s.pageNumber}</span>
                        </div>
                        <p>{s.content.slice(0, 220)}...</p>
                      </div>
                    ))}
                  </details>
                )}
              </div>
            </div>
          </div>
        ))}
        <div ref={bottomRef} />
      </div>

      <form className="composer" onSubmit={handleSubmit}>
        <input
          placeholder="Ask a question about your documents..."
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
        />
        <button type="submit" className="btn-primary" disabled={loading || !question.trim()}>
          {loading ? <span className="spinner light" /> : 'Ask ➤'}
        </button>
      </form>
    </section>
  )
}
