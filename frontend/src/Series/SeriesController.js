var NzbDroneController = require('Shared/NzbDroneController');
var SeriesCollection = require('./SeriesCollection');
var SeriesIndexLayout = require('./Index/SeriesIndexLayout');
var SeriesDetailsLayout = require('./Details/SeriesDetailsLayout');

module.exports = NzbDroneController.extend({
  initialize() {
    this.route('', this.series);
    this.route('series', this.series);
    this.route('series/:query', this.seriesDetails);

    NzbDroneController.prototype.initialize.apply(this, arguments);
  },

  series() {
    this.setTitle('Sonarr');
    this.showMainRegion(new SeriesIndexLayout());
  },

  seriesDetails(query) {
    var series = SeriesCollection.where({ titleSlug: query });

    if (series.length) {
      var targetSeries = series[0];
      this.setTitle(targetSeries.get('title'));
      this.showMainRegion(new SeriesDetailsLayout({ model: targetSeries }));
    } else {
      this.showNotFound();
    }
  }
});