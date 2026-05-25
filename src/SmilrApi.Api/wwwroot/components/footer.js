class SiteFooter extends HTMLElement {
  connectedCallback() {
    this.innerHTML = `
      <footer class="footer">
        <div class="container">
          <p>Data provided by <a href="https://www.foedevarestyrelsen.dk" target="_blank" rel="noopener">Fødevarestyrelsen</a> under Danish open data licence.</p>
          <p>Questions? <a href="mailto:yourSmilr@gmail.com">yourSmilr@gmail.com</a></p>
          <p>
            <a href="/about.html">About us</a> &nbsp;·&nbsp;
            <a href="/contact.html">Contact us</a> &nbsp;·&nbsp;
            <a href="/scores.html">Scores explained</a> &nbsp;·&nbsp;
            <a href="/terms.html">Terms</a> &nbsp;·&nbsp;
            <a href="/privacy.html">Privacy</a>
          </p>
        </div>
      </footer>`;
  }
}
customElements.define('site-footer', SiteFooter);
