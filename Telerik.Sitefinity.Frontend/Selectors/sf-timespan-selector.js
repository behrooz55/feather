﻿(function ($) {
    angular.module('sfSelectors')
        .directive('sfTimespanSelector', ['$timeout', function ($timeout) {

            return {
                restrict: 'E',
                transclude: true,
                scope: {
                    selectedItem: '=?',                   
                    change: '='
                },
                templateUrl: function (elem, attrs) {
                    var assembly = attrs.templateAssembly || 'Telerik.Sitefinity.Frontend';
                    var url = attrs.templateUrl || 'Selectors/sf-timespan-selector.html';
                    return sitefinity.getEmbeddedResourceUrl(assembly, url);
                },
                link: {
                    pre: function (scope, element, attrs, ctrl, transclude) {
                        // ------------------------------------------------------------------------
                        // helper methods
                        // ------------------------------------------------------------------------
                        formatTimeSpanItem = function (item) {
                            if (!item)
                                return;

                            if (item.periodType == 'periodToNow')
                                item.displayText = 'Last ' + item.timeSpanValue + ' ' + item.timeSpanInterval;
                            else if (item.periodType == "customRange") {

                                if (item.fromDate && item.toDate)
                                    item.displayText = "From " + _getFormatedDate(item.fromDate) + " to " + _getFormatedDate(item.toDate);
                                else if (item.fromDate)
                                    item.displayText = "From " + _getFormatedDate(item.fromDate);
                                else if (item.toDate)
                                    item.displayText = "To " + _getFormatedDate(item.toDate);
                            }
                            else
                                item.displayText = 'Any time';
                        };

                        _getFormatedDate = function (date) {
                            if (!date)
                                return;

                            var options = { day: "numeric", month: "short", year: "numeric", hour12: false };
                        
                            if (date.getHours() !== 0 || date.getMinutes() !== 0) {
                                options.hour = "numeric";
                                options.minute = "numeric";
                            }

                            var result = date.toLocaleString("en-GB", options);

                            return result;
                        };

                        clearErrors = function () {
                            scope.showError = false;
                            scope.errorMessage = '';
                        };

                        validate = function (item) {
                            if (item.periodType == 'periodToNow' && !item.timeSpanValue) {
                                scope.errorMessage = 'Invalid period!';
                                scope.showError = true;

                                return false;
                            }
                            else if (item.periodType == 'customRange' && item.fromDate && item.toDate) {
                                var isValid = item.fromDate < item.toDate;

                                if (!isValid) {
                                    scope.errorMessage = 'Invalid date range! The expiration date must be after the publication date.';
                                    scope.showError = true;
                                }
                                else {
                                    clearErrors();
                                }

                                return isValid;
                            }
                            else {
                                clearErrors();

                                return true;
                            }
                        };

                        // ------------------------------------------------------------------------
                        // Scope variables and setup
                        // ------------------------------------------------------------------------                      

                        scope.selectItem = function () {
                            if (validate(scope.selectedItemInTheDialog)) {
                                formatTimeSpanItem(scope.selectedItemInTheDialog);

                                if (scope.change) {
                                    var changeArgs = {
                                        'newSelectedItem': scope.selectedItemInTheDialog,
                                        'oldSelectedItem': jQuery.extend(true, {}, scope.selectedItem)
                                    };
                                    scope.change.call(scope.$parent, changeArgs);
                                }

                                scope.selectedItem = scope.selectedItemInTheDialog;

                                scope.$modalInstance.close();
                            }
                        };

                        scope.isItemSelected = function () {

                            if (scope.selectedItem) {
                                return scope.selectedItem.displayText !== "";
                            }

                            return false;
                        };

                        scope.cancel = function () {
                            try {
                                scope.showError = false;
                                scope.errorMessage = '';
                                scope.$modalInstance.close();
                            } catch (e) { }
                        };

                        scope.open = function () {
                            scope.$openModalDialog();

                            scope.showError = false;
                            scope.errorMessage = '';
                            scope.selectedItemInTheDialog = jQuery.extend(true, {}, scope.selectedItem);
                        };

                        formatTimeSpanItem(scope.selectedItem);
                    }
                }
            };
        }]);
})(jQuery);