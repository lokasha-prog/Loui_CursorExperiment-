// The only settings of the website. Change them here, not in the page code.
window.SITE_CONFIG = {
  // Which centre receives the enquiries (the centre's web name inside NMS).
  centreSlug: 'al-salam-early-childhood',
  // Where the NMS server is. '' means the same address as this website (the usual set-up behind one web server).
  apiBase: '',
  // Optional contact details shown in the footer when filled in. A phone number also turns on the WhatsApp button.
  phone: '',
  email: '',
  address: { en: '', ar: '' },
  // Where the Sign in buttons go: the NMS web app's sign-in page. '' means /login on the same address as this website.
  portalUrl: 'http://localhost:5173/login',
  // Optional: a line shown above the header, for example an open day. { en: '...', ar: '...', link: '#enquire' }
  notice: null,
  // Optional: REAL parent comments only, with their permission. [{ name: 'A. Parent', text: { en: '...', ar: '...' } }]. Empty hides the section.
  testimonials: [],
  // Optional: real photos for the gallery (files you add to the website folder). [{ src: 'photos/art.jpg', label: { en: 'Art morning', ar: 'صباح الفن' } }]. Empty keeps the illustrated tiles.
  photos: [],
}
