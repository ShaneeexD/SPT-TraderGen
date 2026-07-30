import { useState, useEffect, useMemo, useRef } from 'react'
import { Search } from 'lucide-react'

export interface SearchableSelectOption {
  value: string
  label: string
  sub: string
}

interface SearchableSelectProps {
  value: string
  onChange: (value: string) => void
  options: SearchableSelectOption[]
  placeholder?: string
}

export default function SearchableSelect({ value, onChange, options, placeholder }: SearchableSelectProps) {
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const containerRef = useRef<HTMLDivElement>(null)
  const dropdownMouseDown = useRef(false)
  const selected = options.find(o => o.value === value)

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [])

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return options.slice(0, 50)
    return options.filter(o =>
      o.label.toLowerCase().includes(q) ||
      o.sub.toLowerCase().includes(q) ||
      o.value.toLowerCase().includes(q)
    ).slice(0, 100)
  }, [options, query])

  const customId = query.trim()
  const showCustomOption = customId.length === 24 && !options.some(o => o.value.toLowerCase() === customId.toLowerCase())
  const allOptions = showCustomOption ? [...filtered, { value: customId, label: 'Use custom ID', sub: customId }] : filtered

  const commitCustom = (id: string) => {
    onChange(id)
    setQuery(id)
    setOpen(false)
  }

  const handleBlur = () => {
    if (dropdownMouseDown.current) {
      dropdownMouseDown.current = false
      return
    }
    if (customId.length === 24 && customId.toLowerCase() !== value.toLowerCase()) {
      commitCustom(customId)
    } else {
      setOpen(false)
    }
  }

  return (
    <div ref={containerRef} className="relative">
      <div className="relative">
        <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-tarkov-text-dim pointer-events-none" />
        <input
          className="input-field w-full pl-9 font-mono text-sm"
          placeholder={placeholder}
          value={open ? query : selected?.label || value}
          onFocus={() => {
            setQuery(selected?.label || value)
            setOpen(true)
          }}
          onChange={(e) => {
            setQuery(e.target.value)
            setOpen(true)
          }}
          onBlur={handleBlur}
        />
      </div>
      {open && (
        <div
          className="absolute z-50 mt-1 w-full max-h-60 overflow-y-auto bg-tarkov-surface border border-tarkov-border rounded shadow-lg"
          onMouseDown={() => { dropdownMouseDown.current = true }}
          onMouseUp={() => { dropdownMouseDown.current = false }}
        >
          {allOptions.length === 0 ? (
            <div className="px-3 py-2 text-sm text-tarkov-text-dim">No matches</div>
          ) : (
            allOptions.map((o) => (
              <button
                key={o.value + (o.label === 'Use custom ID' ? '-custom' : '')}
                className="w-full text-left px-3 py-2 text-sm hover:bg-tarkov-border/50 text-tarkov-text"
                onClick={() => {
                  if (o.label === 'Use custom ID') {
                    commitCustom(o.value)
                  } else {
                    onChange(o.value)
                    setQuery(o.label)
                    setOpen(false)
                  }
                }}
              >
                <div className="truncate">{o.label}</div>
                <div className="text-xs text-tarkov-text-dim font-mono truncate">{o.sub}</div>
              </button>
            ))
          )}
        </div>
      )}
    </div>
  )
}
