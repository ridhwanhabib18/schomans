import { describe, expect, it } from 'vitest'
import { paragraphsToHtml } from './drafts'

describe('paragraphsToHtml', () => {
  it('wraps every paragraph in its own p element', () => {
    expect(paragraphsToHtml(['One.', 'Two.'])).toBe('<p>One.</p><p>Two.</p>')
  })

  it('escapes markup so a draft can never inject any', () => {
    expect(paragraphsToHtml(['<script>alert("x")</script> & more'])).toBe('<p>&lt;script&gt;alert(&quot;x&quot;)&lt;/script&gt; &amp; more</p>')
  })

  it('gives nothing for no paragraphs', () => {
    expect(paragraphsToHtml([])).toBe('')
  })
})
