var BlacklistModel = require('./BlacklistModel');
var PageableCollection = require('backbone.paginator');
var AsSortedCollection = require('Mixins/AsSortedCollection');
var AsPersistedStateCollection = require('Mixins/AsPersistedStateCollection');

var Collection = PageableCollection.extend({
  url: '/blacklist',
  model: BlacklistModel,

  state: {
    pageSize: 15,
    sortKey: 'date',
    order: 1
  },

  queryParams: {
    totalPages: null,
    totalRecords: null,
    pageSize: 'pageSize',
    sortKey: 'sortKey',
    order: 'sortDir',
    directions: {
      '-1': 'asc',
      '1': 'desc'
    }
  },

  sortMappings: {
    'series': { sortKey: 'series.sortTitle' }
  },

  parseState(resp) {
    return { totalRecords: resp.totalRecords };
  },

  parseRecords(resp) {
    if (resp) {
      return resp.records;
    }

    return resp;
  }
});
Collection = AsSortedCollection.call(Collection);
Collection = AsPersistedStateCollection.call(Collection);

module.exports = Collection;