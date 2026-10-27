import { useEffect, useState } from 'react'
import { getDocuments, uploadDocument, deleteDocument } from './api'

export default function Documents({ selected, onSelect }) {
  const [documents, setDocuments] = useState([])
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState('')

  async function load() {
    setDocuments(await getDocuments())
  }

  useEffect(() => {
    load()
  }, [])

  async function handleUpload(e) {
    const file = e.target.files[0]
    if (!file) return
    setUploading(true)
    setError('')
    try {
      await uploadDocument(file)
      await load()
    } catch (err) {
      setError(err.message)
    }
    setUploading(false)
    e.target.value = '' // allow uploading the same file again
  }

  async function handleDelete(doc) {
    if (!confirm(`Delete "${doc.fileName}"?`)) return
    await deleteDocument(doc.id)
    if (selected?.id === doc.id) onSelect(null)
    await load()
  }

  return (
    <aside className="sidebar">
      <label className={`upload ${uploading ? 'busy' : ''}`}>
        {uploading ? (
          <><span className="spinner" /> Processing PDF...</>
        ) : (
          <><span className="plus">+</span> Upload PDF</>
        )}
        <input type="file" accept=".pdf" onChange={handleUpload} disabled={uploading} hidden />
      </label>
      {error && <p className="error small">{error}</p>}

      <p className="section-title">Search in</p>
      <ul className="doc-list">
        <li className={selected === null ? 'active' : ''} onClick={() => onSelect(null)}>
          <span className="doc-icon all">≡</span>
          <div className="doc-info">
            <span className="doc-name">All documents</span>
            <span className="doc-meta">{documents.length} files</span>
          </div>
        </li>

        {documents.map((doc) => (
          <li key={doc.id} className={selected?.id === doc.id ? 'active' : ''} onClick={() => onSelect(doc)}>
            <span className="doc-icon">PDF</span>
            <div className="doc-info">
              <span className="doc-name" title={doc.fileName}>{doc.fileName}</span>
              <span className="doc-meta">
                {doc.pageCount} {doc.pageCount === 1 ? 'page' : 'pages'} · {new Date(doc.uploadedAt).toLocaleDateString()}
              </span>
            </div>
            <button className="delete" title="Delete" onClick={(e) => { e.stopPropagation(); handleDelete(doc) }}>✕</button>
          </li>
        ))}
      </ul>

      {documents.length === 0 && <p className="muted small center">No documents yet</p>}
    </aside>
  )
}
