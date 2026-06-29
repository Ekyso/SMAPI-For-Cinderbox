var smapi = smapi || {};
var app;
smapi.statsPage = function (options) {
    /*********
    ** Configure
    *********/
    const config = {
        // default colors for chart series (which cycle through the list)
        colorPalette: [
            "#117733", // dark green
            "#332288", // dark indigo
            "#88ccee", // azure
            "#cc6677", // muted rose
            "#ddcc77", // gold
            "#44aa99", // turquoise
            "#aa4499"  // magenta
        ],

        // configure the 'mods by type' stats
        modsByType: {
            // display names for each mod type (omitted mod types are shown as-is)
            labels: {
                "SMAPI": "SMAPI (C#)",
                "Pathoschild.ContentPatcher": "Content Patcher",
                "XNB": "XNB",
                "PeacefulEnd.AlternativeTextures": "Alternative Textures",
                "spacechase0.JsonAssets": "Json Assets",
                "PeacefulEnd.FashionSense": "Fashion Sense",
                "Digus.ProducerFrameworkMod": "Producer Framework Mod",
                "DIGUS.MailFrameworkMod": "Mail Framework Mod",
                "Esca.FarmTypeManager": "Farm Type Manager",
                "Cherry.ShopTileFramework": "Shop Tile Framework",
                "Platonymous.TMXLoader": "TMXL",
                "cat.betterartisangoodicons": "Better Artisan Good Icons",
                "spacechase0.DynamicGameAssets": "Dynamic Game Assets",
                "Platonymous.CustomFurniture": "Custom Furniture",
                "Platonymous.CustomMusic": "Custom Music",
                "leroymilo.FurnitureFramework": "Furniture Framework",
                "Paritee.BetterFarmAnimalVariety": "Better Farm Animal Variety",
                "other": "other (<100 packs)"
            },

            // colors for each mod type (omitted mod types cycle through the colorPalette)
            colors: {
                "other": "#c0c0c8" // silver-gray
            },

            // notable events which may affect stats, indexed by their date in mods-by-type.jsonl
            notableEvents: {
                "2021-01-01": "Stardew Valley 1.5 (21 December 2020)",
                "2021-09-01": "Stardew Valley 1.5.5 (17 August 2021)",
                "2024-04-26": "Stardew Valley 1.6 (19 March 2024)",
                "2024-11-29": "Stardew Valley 1.6.9 (04 November 2024)"
            }
        }
    };


    /*********
    ** Set up state
    *********/
    const data = {
        modsByType: {
            rows: [],
            lastRow: {},
            xLabels: [],
            modTypes: []
        },
        newMods: {
            deltas: [],
            xLabels: []
        }
    };
    const charts = [];


    /*********
    ** Init app
    *********/
    app = new Vue({
        el: "#app",
        data: {
            isDataLoaded: false,
            isDataFailed: false,
            showAdvancedControls: false,

            modsByType: {
                previouslyUpdated: null,
                lastUpdated: null,
                summary: {
                    total: 0,
                    topThree: [],
                    xnbPercent: "0"
                }
            },
            latestNewMods: {
                total: 0,
                byType: [],
                forOther: 0
            }
        },
        watch: {
            showAdvancedControls: function () {
                if (this.isDataLoaded)
                    this.createChartsAsync();
            }
        },
        mounted: function () {
            this.loadAsync();
        },
        methods: {
            /**
             * Fetch the raw data files, initialize the dataset, and build the page's charts.
             */
            loadAsync: async function () {
                await this.loadDataAsync();
                await this.createChartsAsync();

                window.addEventListener("resize", () => resizeCharts(charts));
            },

            /**
             * Fetch the raw data files and initialize the dataset.
             */
            loadDataAsync: async function () {
                /*********
                ** Fetch mods by type
                *********/
                {
                    // fetch dataset
                    const rows = await fetchJsonLinesAsync(options.modsByTypeUri);
                    if (!rows) {
                        this.isDataFailed = true;
                        return;
                    }

                    // derive dataset info
                    const lastRow = rows[rows.length - 1];
                    data.modsByType = {
                        previouslyUpdated: formatFullDate(rows[rows.length - 2].date),
                        lastUpdated: formatFullDate(lastRow.date),

                        rows,
                        lastRow,
                        xLabels: rows.map(row => row.date),
                        modTypes: getModTypes(rows)
                    };
                    this.modsByType.previouslyUpdated = data.modsByType.previouslyUpdated;
                    this.modsByType.lastUpdated = data.modsByType.lastUpdated;

                    // summarize mods by type
                    {
                        const curData = data.modsByType;
                        const total = curData.modTypes.reduce((sum, modType) => sum + (curData.lastRow[modType] ?? 0), 0);

                        const topThree = curData.modTypes
                            .filter(isSpecificModType)
                            .slice(0, 3)
                            .map(modType => ({ label: getLabel(config.modsByType.labels, modType), percent: getPercent(curData.lastRow[modType], total) }));

                        this.modsByType.summary = {
                            total,
                            topThree,
                            xnbPercent: getPercent(curData.lastRow["XNB"], total)
                        };
                    }

                    // assign colors for each mod type
                    let index = 0;
                    for (const modType of data.modsByType.modTypes) {
                        if (config.modsByType.colors[modType])
                            continue; // assigned in config

                        config.modsByType.colors[modType] = config.colorPalette[index++ % config.colorPalette.length];
                    }
                }

                /*********
                ** Calculate 'new mods this month' stats
                *********/
                {
                    const curData = data.modsByType;

                    // exclude historical manual adjustments (e.g. from mod dump rebuilds)
                    const excludeDates = new Set(["2021-03-06", "2022-01-01", "2023-05-04", "2024-08-02"]);
                    const excludeRow = row => excludeDates.has(row.date);

                    // get total new mods per month (skip first row which has no delta)
                    const totals = curData.rows.map(row => curData.modTypes.reduce((curTotal, modType) => curTotal + (row[modType] ?? 0), 0));
                    const deltas = curData.rows
                        .slice(1)
                        .map((_, i) => totals[i + 1] - totals[i])
                        .filter((_, i) => !excludeRow(curData.rows[i + 1]));
                    const filteredXLabels = curData.xLabels
                        .slice(1)
                        .filter((_, i) => !excludeRow(curData.rows[i + 1]));
                    const lastDelta = deltas[deltas.length - 1];

                    // get per-type deltas for last month
                    const lastRow = curData.lastRow;
                    const prevRow = curData.rows[curData.rows.length - 2];
                    const byType = curData.modTypes
                        .filter(isSpecificModType)
                        .map(modType => ({
                            type: modType,
                            label: getLabel(config.modsByType.labels, modType),
                            delta: (lastRow[modType] ?? 0) - (prevRow[modType] ?? 0)
                        }))
                        .sort((a, b) => b.delta - a.delta);
                    const otherDelta = (lastRow["other"] ?? 0) - (prevRow["other"] ?? 0);

                    // save values
                    data.newMods = {
                        deltas: deltas,
                        xLabels: filteredXLabels
                    };
                    this.latestNewMods = {
                        total: lastDelta,
                        byType: byType,
                        forOther: otherDelta
                    };
                }


                /*********
                ** Mark data ready
                *********/
                this.isDataLoaded = true;
            },

            /**
             * Create (or recreate) the charts on the page.
             */
            createChartsAsync: async function () {
                /*********
                ** Reset
                *********/
                // destroy any current charts
                for (const chart of charts)
                    chart.dispose();
                charts.length = 0;

                // wait for Vue to render chart containers if needed
                await this.$nextTick();


                /*********
                ** 'Mods by type' pie chart
                *********/
                {
                    const curData = data.modsByType;
                    const lastRowIndex = curData.rows.length - 1;

                    const chart = echarts.init(document.getElementById("modsByType"));

                    chart.setOption({
                        baseOption: {
                            series: [
                                {
                                    type: "pie",
                                    radius: "80%",
                                    clockwise: false // start with larger values on the left
                                }
                            ],
                            timeline: createTimeline(curData.xLabels, this.showAdvancedControls, config.modsByType.notableEvents),
                            toolbox: createToolbox(false, this.showAdvancedControls)
                        },
                        options: curData.rows.map((row, i) => {
                            const isLatest = i === lastRowIndex;
                            const total = curData.modTypes.reduce((sum, type) => sum + (row[type] ?? 0), 0);

                            return {
                                title: {
                                    text: getTimelineTitle("Total mods by type", row.date, isLatest),
                                    textStyle: createTitleStyle()
                                },
                                series: [
                                    {
                                        label: {
                                            formatter: entry => `${entry.name}  ${getPercent(entry.value, total)}%`
                                        },
                                        data: curData.modTypes.map(modType => ({
                                            name: getLabel(config.modsByType.labels, modType),
                                            value: row[modType] ?? 0,
                                            label: {
                                                show: !!row[modType]
                                            },
                                            itemStyle: {
                                                color: getColor(config.modsByType.colors, modType)
                                            }
                                        }))
                                    }
                                ],
                                tooltip: {
                                    formatter: entry => entry.name && entry.value
                                        ? `${entry.name}: ${entry.value.toLocaleString()} (${getPercent(entry.value, total)}%)`
                                        : null // not a slice (e.g. a timeline control)
                                },
                                animation: isLatest // don't animate previous rows, which prevents the chart from updating while playing/scrolling timeline
                            };
                        })
                    });

                    charts.push(chart);
                }


                /*********
                ** 'Total mods by type' line charts
                *********/
                {
                    const curData = data.modsByType;
                    const lastRowIndex = curData.rows.length - 1;

                    const chartConfigs = [
                        {
                            id: "modsByTypeOverTime",
                            title: "Mods by type over time",
                            modTypes: curData.modTypes.filter(isSpecificModType)
                        },
                        {
                            id: "modsByTypeOverTimeExcludingOutliers",
                            title: "Mods by type over time (excluding SMAPI and Content Patcher)",
                            modTypes: curData.modTypes.filter(k => isSpecificModType(k) && k !== "SMAPI" && k !== "Pathoschild.ContentPatcher")
                        }
                    ];

                    for (const chartConfig of chartConfigs) {
                        const chart = echarts.init(document.getElementById(chartConfig.id));

                        chart.setOption({
                            baseOption: {
                                legend: {
                                    show: false
                                },
                                grid: {
                                    top: 40,
                                    bottom: this.showAdvancedControls ? 100 : 60, // make room for timeline
                                    left: 60,
                                    right: 150 // extra right margin to fit end labels
                                },
                                xAxis: {
                                    data: [], // overridden in `options`
                                    axisLabel: {
                                        rotate: 90,
                                        fontSize: 11,
                                        formatter: value => value.slice(0, 7)
                                    }
                                },
                                yAxis: {},
                                series: chartConfig.modTypes.map(modType => {
                                    const extendsToEnd = !!curData.lastRow[modType];
                                    const rawColor = getColor(config.modsByType.colors, modType);
                                    const color = extendsToEnd
                                        ? rawColor
                                        : hexToRgba(rawColor, 0.6);

                                    return {
                                        type: "line",
                                        name: getLabel(config.modsByType.labels, modType),
                                        data: [], // overridden in `options`
                                        color: color,
                                        lineStyle: {
                                            type: extendsToEnd ? "solid" : "dotted",
                                            width: 2
                                        },
                                        symbol: "none",
                                        endLabel: {
                                            show: true,
                                            formatter: "{a}", // {a} = name
                                            color: color
                                        },
                                        labelLayout: {
                                            moveOverlap: extendsToEnd
                                                ? "shiftY" // if the line reaches the end of the chart, use 'shiftY' layout to avoid overlapping labels
                                                : null     // if the line ends early, just display it next to the line instead
                                        }
                                    };
                                }),
                                tooltip: {
                                    trigger: "axis"
                                },
                                toolbox: createToolbox(true, this.showAdvancedControls),
                                timeline: createTimeline(curData.xLabels, this.showAdvancedControls, config.modsByType.notableEvents)
                            },
                            options: curData.rows.map((row, i) => {
                                const isLatest = i === lastRowIndex;

                                return {
                                    title: {
                                        text: getTimelineTitle(chartConfig.title, row.date, isLatest),
                                        textStyle: createTitleStyle()
                                    },
                                    xAxis: {
                                        data: curData.xLabels.slice(0, i + 1) // skip first row, since it has no previous row to delta against
                                    },
                                    series: chartConfig.modTypes.map(modType => {
                                        const data = curData.rows.slice(0, i + 1).map(row => row[modType] ?? null);

                                        return {
                                            data,
                                            endLabel: {
                                                show: !!data[data.length - 1]
                                            }
                                        };
                                    }),
                                    animation: isLatest
                                };
                            })
                        });

                        charts.push(chart);
                    }
                }


                /*********
                ** 'New mods' pie chart
                *********/
                {
                    const curData = data.modsByType;
                    const lastRowIndex = curData.rows.length - 2; // rendered rows start at row 1 (since they delta against previous row)

                    const chart = echarts.init(document.getElementById("newModsLastDelta"));

                    chart.setOption({
                        baseOption: {
                            series: [
                                {
                                    type: "pie",
                                    radius: "80%",
                                    clockwise: false, // start with larger values on the left
                                    label: {
                                        formatter: entry => `${entry.name}  ${entry.percent.toFixed(1)}%`
                                    },
                                    data: [] // overridden in `options`
                                }
                            ],
                            tooltip: {
                                formatter: entry => `${entry.name}: ${entry.value.toLocaleString()} (${entry.percent.toFixed(1)}%)`
                            },
                            toolbox: createToolbox(false, this.showAdvancedControls),
                            timeline: createTimeline(
                                curData.xLabels.slice(1),
                                this.showAdvancedControls,
                                config.modsByType.notableEvents,
                                {
                                    controlStyle: {
                                        showPlayBtn: false // hide play button, since the data is too erratic for autoplay to be useful
                                    }
                                }
                            )
                        },
                        options: curData.rows.slice(1).map((row, i) => {
                            const prevRow = curData.rows[i];
                            const isLatest = i === lastRowIndex;

                            return {
                                title: {
                                    text: `New mods between ${formatFullDate(prevRow.date)} and ${formatFullDate(row.date)}`,
                                    textStyle: createTitleStyle()
                                },
                                series: [
                                    {
                                        data: curData.modTypes
                                            .map(modType => ({
                                                type: modType,
                                                delta: (row[modType] ?? 0) - (prevRow[modType] ?? 0)
                                            }))
                                            .filter(entry => entry.delta > 0)
                                            .map(entry => ({
                                                name: getLabel(config.modsByType.labels, entry.type),
                                                value: entry.delta,
                                                itemStyle: {
                                                    color: getColor(config.modsByType.colors, entry.type)
                                                }
                                            }))
                                    }
                                ],
                                animation: isLatest
                            };
                        })
                    });

                    charts.push(chart);
                }


                /*********
                ** 'New mods' bar chart
                *********/
                {
                    const chart = echarts.init(document.getElementById("newMods"));
                    const lastRowIndex = data.newMods.xLabels.length - 1;

                    chart.setOption({
                        baseOption: {
                            legend: {
                                show: false
                            },
                            grid: {
                                top: 40,
                                bottom: this.showAdvancedControls ? 100 : 60, // make room for timeline
                                left: 60,
                                right: 60
                            },
                            xAxis: {
                                data: data.newMods.xLabels, // base data; overridden per frame in options[]
                                axisLabel: {
                                    rotate: 90,
                                    fontSize: 11,
                                    formatter: value => value.slice(0, 7)
                                }
                            },
                            yAxis: {},
                            series: [
                                {
                                    type: "bar",
                                    data: [] // placeholder; overridden per frame in options[]
                                }
                            ],
                            tooltip: {
                                trigger: "axis"
                            },
                            toolbox: createToolbox(true, this.showAdvancedControls),
                            timeline: createTimeline(data.newMods.xLabels, this.showAdvancedControls, config.modsByType.notableEvents)
                        },
                        options: data.newMods.xLabels.map((xLabel, i) => {
                            const isLatest = i === lastRowIndex;

                            return {
                                title: {
                                    text: getTimelineTitle("New mods by month", xLabel, isLatest),
                                    textStyle: createTitleStyle()
                                },
                                xAxis: {
                                    data: data.newMods.xLabels.slice(0, i + 1)
                                },
                                series: [
                                    {
                                        data: data.newMods.deltas.slice(0, i + 1)
                                    }
                                ],
                                animation: isLatest
                            };
                        })
                    });
                    charts.push(chart);
                }
            },

            /**
             * Format a numeric change for display, with comma separators and sign.
             * @param {number} value The number to format.
             * @returns {string} Returns the formatted number.
             */
            formatDelta: function (value) {
                return value > 0
                    ? `+${value.toLocaleString()}`
                    : value.toLocaleString();
            }
        }
    });


    /*********
    ** Helper methods
    *********/
    /****
    ** Generic data
    ****/
    /**
     * Fetch and parse data from a JSONL URI.
     * @param {string} fetchUri The URI from which to fetch the JSONL data.
     * @returns {null|array<object>} Returns an array of parsed objects if the data was found, else `null`.
     */
    async function fetchJsonLinesAsync(fetchUri) {
        const response = await fetch(fetchUri);
        if (!response.ok)
            return null;

        const rawJsonLines = await response.text();
        return rawJsonLines.trim().split(/\r?\n/).filter(row => !!row).map(JSON.parse);
    }

    /**
     * Get a formatted date string.
     * @param {Date|string} date The ISO 8601 date-only string to format.
     * @param {Intl.DateTimeFormatOptions} options The date format options.
     */
    function formatDate(date, options) {
        if (typeof date === "string")
            date = new Date(date + "T12:00"); // set time to noon to avoid any timezone conversions changing the date parts

        return new Intl.DateTimeFormat("en-GB", options).format(date);
    }

    /**
     * Get a formatted date string in the form 'dd MMM yyyy', like '31 January 2030'.
     * @param {Date|string} date The ISO 8601 date-only string to format.
     */
    function formatFullDate(date) {
        return formatDate(date, { dateStyle: "long" });
    }

    /**
     * Get the display name for a data key.
     * @param {object} lookup The name dictionary to check.
     * @param {string} key The data key.
     * @returns {string}
     */
    function getLabel(lookup, key) {
        return lookup[key] ?? key;
    }

    /**
     * Get the hexadecimal color code for a data key.
     * @param {object} lookup The color dictionary to check.
     * @param {string} key The data key.
     * @returns {string}
     */
    function getColor(lookup, key) {
        return lookup[key] ?? "#808080";
    }

    /**
     * Get a single-decimal percentage value.
     * @param {number|null|undefined} value The value for which to get a percentage.
     * @param {number} total The denominator for the value.
     * @returns {string}
     */
    function getPercent(value, total) {
        if (!value)
            value = 0;

        return (Math.round(value / total * 1000) / 10).toString();
    }

    /**
     * Get an RGBA representation of a hexadecimal color.
     * @param {string} hex The hexadecimal color code, including the '#' prefix.
     * @param {number} alpha The alpha channel, as a value between 0 (transparent) and 1 (opaque).
     * @returns
     */
    function hexToRgba(hex, alpha) {
        const r = parseInt(hex.slice(1, 3), 16);
        const g = parseInt(hex.slice(3, 5), 16);
        const b = parseInt(hex.slice(5, 7), 16);
        return `rgba(${r},${g},${b},${alpha})`;
    }

    /****
    ** Mods-by-type data
    ****/
    /**
     * Get the unique mod types, sorted by their number of mods in the latest row.
     * @param {array<object>} rows The parsed mods-by-type rows over time.
     * @returns {array<string>}
     */
    function getModTypes(rows) {
        const rawModTypes = rows.flatMap(row => Object.keys(row).filter(isModType));
        const uniqueModTypes = [...new Set(rawModTypes)];

        const lastRow = rows[rows.length - 1];
        return uniqueModTypes.sort((modTypeA, modTypeB) => {
            // special case: 'other' always at the end
            if (modTypeA === "other")
                return 1;
            if (modTypeB === "other")
                return -1;

            // else sort by count descending
            const countA = lastRow[modTypeA] ?? 0;
            const countB = lastRow[modTypeB] ?? 0;
            return countB - countA;
        })
    }

    /**
     * Get whether a key in a data row is a mod type, rather than a metadata field (like 'date' or '@comment').
     * @param {string} key The data key to check.
     */
    function isModType(key) {
        return key !== "date" && key !== "@comment";
    }

    /**
     * Get whether a key in a data row is a strict mod type, rather than a metadata field (like 'date' or '@comment') or 'other'.
     * @param {string} key The data key to check.
     */
    function isSpecificModType(key) {
        return key !== "date" && key !== "@comment" && key !== "other";
    }

    /****
    ** Charts
    ****/
    /**
     * Get the chart title for a given timeline frame.
     * @param {string} baseTitle The base chart title.
     * @param {string|Date} date The date of the current timeline frame.
     * @param {boolean} isLatest Whether the chart is displaying the last frame on the timeline.
     * @returns {string}
     */
    function getTimelineTitle(baseTitle, date, isLatest) {
        return isLatest
            ? baseTitle
            : `${baseTitle} (${formatFullDate(date)})`;
    }

    /**
     * Create the default chart title style.
     */
    function createTitleStyle() {
        return {
            fontFamily: "Roboto",
            fontSize: 13
        };
    }

    /**
     * Create the timeline options for a chart, given its x-axis labels and optional date markers.
     * @param {array<string>} xLabels The x-axis labels.
     * @param {boolean} show Whether the timeline controls should be visible.
     * @param {array<object>} markers The date markers to display on the timeline, if any. This should be a lookup of `xLabels` key to tooltip text.
     * @param {object|null} customOptions the custom ECharts timeline options to merge into the default options, if any.
     */
    function createTimeline(xLabels, show, markers, customOptions) {
        return {
            show,
            axisType: "category",
            data: xLabels.map(xLabel => {
                const marker = markers[xLabel];
                return marker
                    ? {
                        value: xLabel,
                        symbol: "diamond",
                        tooltip: {
                            formatter: () => marker
                        }
                    }
                    : {
                        value: xLabel,
                        symbol: "none"
                    };
            }),
            currentIndex: xLabels.length - 1, // default to latest row
            playInterval: 200,
            loop: false,
            realtime: true,
            label: {
                show: false
            },
            ...customOptions
        };
    }

    /**
     * Create the toolbox options for a chart.
     * @param {boolean} isLinearChart Whether the options are for a linear chart (e.g. a bar chart or line chart).
     * @param {boolean} show Whether to show advanced controls.
     */
    function createToolbox(isLinearChart, showAdvancedControls = true) {
        const toolbox = {
            feature: {
                dataView: {
                    show: showAdvancedControls,
                    readOnly: true
                },
                saveAsImage: {
                    excludeComponents: ["timeline", "toolbox"]
                }
            }
        };

        if (isLinearChart && showAdvancedControls)
            toolbox.feature.dataZoom = {};

        return toolbox;
    }

    /**
     * Resize all charts to match their container size.
     * @param {array<object>} charts The charts to resize.
     */
    function resizeCharts(charts) {
        for (const chart of charts)
            chart.resize();
    }
};
