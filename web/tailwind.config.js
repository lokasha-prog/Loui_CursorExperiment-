/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: { terracotta: '#E07A5F', sage: '#81B29A', amber: '#E9C46A' },
    },
  },
  plugins: [],
};
