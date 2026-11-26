(() => {
  const repository = "fadrian06/SimCity-4-Launcher";
  const releasesUrl = `https://github.com/${repository}/releases`;
  const downloadButton = document.querySelector("#download-button");
  const closingDownload = document.querySelector("#closing-download");
  const status = document.querySelector("#release-status");

  async function showLatestBeta() {
    try {
      const response = await fetch(`https://api.github.com/repos/${repository}/releases?per_page=20`, {
        headers: { Accept: "application/vnd.github+json" }
      });
      if (!response.ok) throw new Error("GitHub Releases is unavailable");

      const releases = await response.json();
      const latestBeta = releases.find((release) =>
        release.prerelease && !release.draft &&
        release.assets.some((asset) => asset.name === "SimCity-4-Launcher-beta.zip")
      );
      if (!latestBeta) throw new Error("No published beta release found");

      const asset = latestBeta.assets.find((item) => item.name === "SimCity-4-Launcher-beta.zip");
      downloadButton.href = asset.browser_download_url;
      closingDownload.href = asset.browser_download_url;
      downloadButton.querySelector("span").textContent = `Descargar ${latestBeta.tag_name}`;
      closingDownload.firstChild.textContent = `Descargar ${latestBeta.tag_name} `;
      status.textContent = `Última beta: ${latestBeta.tag_name} · Windows · El juego no está incluido`;
    } catch {
      downloadButton.href = releasesUrl;
      closingDownload.href = releasesUrl;
      downloadButton.querySelector("span").textContent = "Ver versiones beta";
      closingDownload.firstChild.textContent = "Ver versiones beta ";
      status.textContent = "Versiones beta para Windows · El juego no está incluido";
    }
  }

  showLatestBeta();
})();